"""Independent ONNX Runtime reference, exact XML FK, fixtures and timing."""
import argparse
import collections
import json
import pathlib
import time
import xml.etree.ElementTree as ET
import numpy as np
import onnxruntime as ort
from scipy.spatial.transform import Rotation

ROOT = pathlib.Path(__file__).resolve().parents[2]
MODEL_VERSION = 'gear-sonic-6733128a3d8a523b1418b06bca3cdf61c8b0987f'
FPS = 30

def skeleton():
    xml = ET.parse(ROOT / 'External/motionbricks/assets/skeletons/g1/g1_29dof.xml')
    nodes = []; angle = 7
    def visit(body, parent):
        nonlocal angle
        joint = body.find('joint'); hinge = joint is not None and joint.get('type', 'hinge') == 'hinge'
        node = dict(name=body.get('name'), parent=parent,
                    position=[float(x) for x in body.get('pos', '0 0 0').split()],
                    rotation=[float(x) for x in body.get('quat', '1 0 0 0').split()],
                    axis=[float(x) for x in joint.get('axis', '0 0 1').split()] if hinge else [0,0,1],
                    pivot=[float(x) for x in joint.get('pos', '0 0 0').split()] if hinge else [0,0,0],
                    qposIndex=angle if hinge else -1)
        if hinge: angle += 1
        index = len(nodes); nodes.append(node)
        for child in body.findall('body'): visit(child, index)
    visit(xml.find('worldbody/body'), -1)
    assert angle == 36, angle
    return nodes

def fk(qpos, nodes):
    positions=[]; rotations=[]
    for node in nodes:
        if node['parent'] < 0:
            pos=qpos[:3]; rot=Rotation.from_quat(qpos[[4,5,6,3]])
        else:
            p=node['parent']; base=Rotation.from_quat(np.array(node['rotation'])[[1,2,3,0]])
            hinge=Rotation.from_rotvec(np.array(node['axis'])*qpos[node['qposIndex']]) if node['qposIndex']>=0 else Rotation.identity()
            pivot=np.array(node['pivot'])
            offset=np.array(node['position'])+base.apply(pivot-hinge.apply(pivot))
            pos=positions[p]+rotations[p].apply(offset); rot=rotations[p]*base*hinge
        positions.append(pos); rotations.append(rot)
    return np.array(positions), np.array([r.as_quat() for r in rotations])

def initial_context():
    # Canonical height from pinned C++ PlannerConfig; neutral hinge pose is an explicit bootstrap assumption.
    q=np.zeros((1,4,36),np.float32); q[:,:,2]=0.788740; q[:,:,3]=1
    return q

def inputs(context, mode=0, movement=(0,0,0), facing=(1,0,0), seed=1234):
    return dict(context_mujoco_qpos=np.asarray(context,np.float32).reshape(1,4,36),
        target_vel=np.array([-1],np.float32), mode=np.array([mode],np.int64),
        movement_direction=np.array([movement],np.float32), facing_direction=np.array([facing],np.float32),
        random_seed=np.array([seed],np.int64), has_specific_target=np.zeros((1,1),np.int64),
        specific_target_positions=np.zeros((1,4,3),np.float32), specific_target_headings=np.zeros((1,4),np.float32),
        allowed_pred_num_tokens=np.array([[1,1,1,1,1,1,0,0,0,0,0]],np.int64), height=np.array([-1],np.float32))

class Planner:
    def __init__(self):
        start=time.perf_counter(); options=ort.SessionOptions(); options.intra_op_num_threads=4
        self.session=ort.InferenceSession(str(ROOT/'Models/planner_sonic.onnx'), options, providers=['CPUExecutionProvider'])
        self.load_seconds=time.perf_counter()-start
    def generate(self, feed):
        start=time.perf_counter(); q,n=self.session.run(None,feed); ms=(time.perf_counter()-start)*1000
        count=int(n.reshape(-1)[0])
        if not 4 <= count <= 64 or q.shape != (1,64,36): raise ValueError('Invalid output shape/count')
        q=q[0,:count].copy()
        if not np.isfinite(q).all(): raise ValueError('Non-finite model output')
        norms=np.linalg.norm(q[:,3:7],axis=-1)
        if np.max(abs(norms-1)) > .01: raise ValueError('Non-unit root quaternions')
        return q,ms

def main():
    parser=argparse.ArgumentParser(); parser.add_argument('--repeats',type=int,default=12); args=parser.parse_args()
    out=ROOT/'Artifacts/reference'; out.mkdir(parents=True,exist_ok=True)
    planner=Planner(); nodes=skeleton()
    (out/'skeleton.json').write_text(json.dumps(dict(nodes=nodes),indent=2))
    report=dict(modelVersion=MODEL_VERSION,frameRate=FPS,provider=planner.session.get_providers(),loadSeconds=planner.load_seconds,
        inputs=[dict(name=i.name,shape=i.shape,dtype=i.type) for i in planner.session.get_inputs()],
        outputs=[dict(name=i.name,shape=i.shape,dtype=i.type) for i in planner.session.get_outputs()],cases={})
    context=initial_context(); all_q=[]; warm=[]
    for name,mode,move,face in [('initialize',0,(0,0,0),(1,0,0)),('walk',2,(1,0,0),(1,0,0)),
            ('left',2,(0,1,0),(0,1,0)),('right',2,(0,-1,0),(0,-1,0)),('stop',0,(0,0,0),(1,0,0)),('run',3,(1,0,0),(1,0,0))]:
        feed=inputs(context,mode,move,face); q,cold=planner.generate(feed); repeated=[]
        for _ in range(args.repeats):
            other,ms=planner.generate(feed); warm.append(ms); repeated.append(float(np.max(abs(other-q))))
        np.savez(out/f'{name}.npz',**feed,output=q)
        (out/f'native-{name}.json').write_text(json.dumps(dict(context=feed['context_mujoco_qpos'].reshape(-1).tolist(),
            expected=q.reshape(-1).tolist(),validFrameCount=len(q),mode=mode,movement=list(move),facing=list(face))))
        world=np.array([fk(f,nodes)[0] for f in q])
        report['cases'][name]=dict(validFrames=len(q),firstCallMs=cold,maxRepeatDifference=max(repeated),
            rootDisplacementMeters=float(np.linalg.norm(q[-1,:2]-q[0,:2])),
            maxRootStepMeters=float(np.linalg.norm(np.diff(q[:,:3],axis=0),axis=-1).max()),
            minJointHeight=float(world[:,:,2].min()))
        all_q.extend(q[:-4]); context=q[-4:][None]
        print(name,report['cases'][name],flush=True)
    report['warmP50Ms']=float(np.percentile(warm,50)); report['warmP95Ms']=float(np.percentile(warm,95))
    import psutil
    report['rssMiB']=psutil.Process().memory_info().rss/1024**2
    (out/'report.json').write_text(json.dumps(report,indent=2))
    (out/'motion.json').write_text(json.dumps(dict(modelVersion=MODEL_VERSION,frameRate=FPS,
        frames=[dict(qpos=q.tolist()) for q in all_q])))
    # Numeric FK reference fixture for independent Unity comparison.
    (out/'fk-reference.json').write_text(json.dumps(dict(qpos=all_q[10].tolist(),positions=fk(all_q[10],nodes)[0].reshape(-1).tolist())))
    import matplotlib; matplotlib.use('Agg')
    import matplotlib.pyplot as plt
    fig=plt.figure(figsize=(16,8))
    for i,name in enumerate(report['cases']):
        q=np.load(out/f'{name}.npz')['output']; points,_=fk(q[len(q)//2],nodes)
        ax=fig.add_subplot(2,3,i+1,projection='3d')
        for j,node in enumerate(nodes):
            if node['parent']>=0:
                line=points[[node['parent'],j]]; ax.plot(*line.T,color='tab:blue')
        ax.plot(q[:,0],q[:,1],np.zeros(len(q)),color='tab:orange')
        center=points[0]; ax.set(xlim=(center[0]-1,center[0]+1),ylim=(center[1]-1,center[1]+1),zlim=(0,2),title=name,xlabel='X forward',ylabel='Y left',zlabel='Z up')
    fig.tight_layout(); fig.savefig(out/'source-skeleton.png',dpi=150)
    print(json.dumps({k:report[k] for k in ('loadSeconds','warmP50Ms','warmP95Ms','rssMiB')}))

if __name__=='__main__': main()
