"""Load the pinned reference backbone without constructing/downloading a dataset."""
import os
import sys
import pathlib
import argparse
import json
import traceback
ROOT=pathlib.Path(__file__).resolve().parents[2]
sys.path.insert(0,str(ROOT/'External/motionbricks'))
os.chdir(ROOT/'External/motionbricks')

def main():
    import torch
    from motionbricks.exp_setup.experiment import test
    from motionbricks.motion_backbone.inference.motion_inference import motion_inference
    args=argparse.Namespace(result_dir='./out',data_root='./datasets',explicit_dataset_folder=None,EXP='default',return_model_configs=True,return_dataloader=False)
    print('torch',torch.__version__,'CUDA',torch.cuda.is_available(),flush=True)
    models,confs=test(args)
    for name in ('pose','root'):
        state=torch.load(confs[name].ckpt_path,map_location='cpu',weights_only=False)['state_dict']
        models[name].load_state_dict(state); del state
    inferencer=motion_inference(models,models['pose'].args)
    torch.cuda.synchronize()
    print('BACKBONE_LOADED',torch.cuda.memory_allocated()/1024**2,flush=True)
    probe_pose(inferencer)
    return inferencer

def probe_pose(inferencer):
    import time
    import numpy as np
    import torch as t
    from motionbricks.helper.mujoco_helper import get_mujoco_converter
    from motionbricks.motionlib.core.utils.rotations import matrix_to_cont6d
    sys.path.insert(0,str(ROOT/'Tools/motionbricks_reference'))
    from reference import skeleton, fk
    converter=get_mujoco_converter(inferencer.motion_rep,'assets/skeletons/g1/g1.xml').to('cuda')
    context=np.load(ROOT/'Artifacts/reference/initialize.npz')['output'][-4:]
    target=context[-1].copy();target[:2]=0;target[3:7]=[1,0,0,0];target[7:]=0
    nodes=skeleton()
    for node in nodes:
        if 'hip_pitch' in node['name']: target[node['qposIndex']]=-np.pi/2
        if 'knee' in node['name']: target[node['qposIndex']]=np.pi/2
    target[2]+=.03-fk(target,nodes)[0][:,2].min()
    def features(q):
        pos,rot=converter.convert_mujoco_qpos_to_motion_transforms(q)
        root=pos[:,:,0];heading=t.atan2(rot[:,:,0,0,2],rot[:,:,0,2,2])
        global_root=t.cat([root,t.cos(heading)[...,None],t.sin(heading)[...,None]],-1)
        local_root=t.zeros((1,4,4),device='cuda');local_root[:,:,3]=root[:,:,1]
        local_root[:,:3,0]=((heading[:,1:]-heading[:,:-1]+t.pi)%(2*t.pi)-t.pi)*30
        local_root[:,:3,1:3]=(root[:,1:,[0,2]]-root[:,:-1,[0,2]])*30
        local_root[:,-1,:3]=local_root[:,-2,:3]
        relative=pos[:,:,1:].clone();relative[:,:,:,0]-=root[:,:,None,0];relative[:,:,:,2]-=root[:,:,None,2]
        body=t.cat([relative.flatten(2),matrix_to_cont6d(rot).flatten(2)],-1)
        return global_root,local_root,body
    out=ROOT/'Artifacts/backbone';out.mkdir(exist_ok=True)
    from scipy.spatial.transform import Rotation
    records=[]
    for placement,(x,y,yaw) in enumerate([(0,0,0),(2,1,np.pi/2),(-1,2,-np.pi/4)]):
        def transform(q):
            q=q.copy();r=Rotation.from_euler('z',yaw)
            q[...,:3]=r.apply(q[...,:3])+[x,y,0]
            q[...,3:7]=(r*Rotation.from_quat(q[...,[4,5,6,3]])).as_quat()[...,[3,0,1,2]]
            return q
        ctx=transform(context);goal=transform(target)
        a=features(t.tensor(ctx[None],device='cuda'));b=features(t.tensor(np.tile(goal,(1,4,1)),device='cuda'))
        values=[t.cat([x,y],1) for x,y in zip(a,b)]
        for tokens in (8,12,16):
            masks=[t.ones((1,8),dtype=t.bool,device='cuda') for _ in range(3)];masks[1][:,3]=False
            t.manual_seed(1234);t.cuda.reset_peak_memory_stats();start=time.perf_counter()
            with t.inference_mode():
                pred,n=inferencer.predict(values[0],masks[0],values[1],masks[1],values[2],masks[2],tokens,
                    config={'num_inference_step':1,'smooth_root_traj':False,'allow_pred_out_of_reach_num_tokens':False,'pose_token_sampling_use_argmax':True,'skip_ending_target_cond':False})
                q=converter.convert_motion_features_to_mujoco_qpos(pred,inferencer.motion_rep,False,root_quat_w_first=True)
            t.cuda.synchronize();ms=(time.perf_counter()-start)*1000;count=int(n.flatten()[0])*4;q=q[0,:count].cpu().numpy()
            if not np.isfinite(q).all():raise ValueError('Backbone returned non-finite motion')
            target_pos=fk(goal,nodes)[0];actual=fk(q[-1],nodes)[0]
            error=float(np.linalg.norm(actual-target_pos,axis=-1).max())
            report=dict(placement=placement,tokens=tokens,transform=[x,y,yaw],validFrames=count,
                inferenceMs=ms,peakAllocatedMiB=t.cuda.max_memory_allocated()/1024**2,
                finalJointPositionMaxErrorMeters=error,humanScaledMaxErrorMeters=error*1.4193771,
                finalRootErrorMeters=float(np.linalg.norm(q[-1,:3]-goal[:3])),
                minJointHeightMeters=float(min(fk(frame,nodes)[0][:,2].min() for frame in q)),targetRootHeight=float(goal[2]))
            records.append(report);np.savez(out/f'sit-{placement}-{tokens}.npz',context=ctx,target=goal,output=q)
            if placement==0 and tokens==12:
                np.savez(out/'authored-sit.npz',context=ctx,target=goal,output=q)
                (out/'motion.json').write_text(json.dumps(dict(modelVersion='motionbricks-7f151314',frameRate=30,frames=[dict(qpos=f.tolist()) for f in q])))
            print(json.dumps(report),flush=True)
    (out/'placements.json').write_text(json.dumps(records,indent=2))
    (out/'target.json').write_text(json.dumps(dict(qpos=target.tolist())))
    import matplotlib;matplotlib.use('Agg')
    import matplotlib.pyplot as plt
    fig=plt.figure(figsize=(15,5))
    for i in range(3):
        fixture=np.load(out/f'sit-{i}-12.npz');ax=fig.add_subplot(1,3,i+1,projection='3d')
        for q,color,label in [(fixture['target'],'tab:orange','Authored target'),(fixture['output'][-1],'tab:blue','Generated final pose')]:
            points=fk(q,nodes)[0]
            for j,node in enumerate(nodes):
                if node['parent']>=0:
                    ax.plot(*points[[node['parent'],j]].T,color=color,label=label if j==1 else None)
        ax.set(title=f'Placement {i}: 48 frames',xlabel='X forward',ylabel='Y left',zlabel='Z up');ax.set_box_aspect((1,1,1));ax.legend()
    fig.tight_layout();fig.savefig(out/'chair-pose-comparison.png',dpi=140)

if __name__=='__main__':
    try: main()
    except Exception:
        error=traceback.format_exc(); (ROOT/'Artifacts/backbone-error.txt').write_text(error); raise

