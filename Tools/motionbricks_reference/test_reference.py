import json
import socket
import struct
import unittest
import importlib.util
import xml.etree.ElementTree as ET
import numpy as np
from reference import initial_context, skeleton, fk, inputs, Planner, MODEL_VERSION, ROOT
from service import handle, receive, send, MAX_BYTES

class ProtocolTests(unittest.TestCase):
    def test_bounded_length_and_fragmented_frame(self):
        a,b=socket.socketpair()
        with a,b:
            a.sendall(struct.pack('<I',MAX_BYTES+1))
            with self.assertRaises(ValueError):receive(b)
        a,b=socket.socketpair()
        with a,b:
            send(a,dict(protocolVersion=1,value='test'));self.assertEqual(receive(b)['value'],'test')
    def test_reject_invalid_context_and_version_without_model(self):
        with self.assertRaises(ValueError):handle(None,dict(protocolVersion=2))
        with self.assertRaises(ValueError):handle(None,dict(protocolVersion=1,operation='generate',context=[0]))
    def test_skeleton_hinge_count_and_pelvis(self):
        nodes=skeleton(); self.assertEqual(sorted(n['qposIndex'] for n in nodes if n['qposIndex']>=0),list(range(7,36)))
        q=initial_context()[0,0];positions,_=fk(q,nodes);np.testing.assert_allclose(positions[0],q[:3])
    @unittest.skipUnless(importlib.util.find_spec('mujoco'),'Install backbone dependencies for independent MuJoCo FK validation')
    def test_actual_mujoco_fk(self):
        import mujoco
        from scipy.spatial.transform import Rotation
        xml=ET.parse(ROOT/'External/motionbricks/assets/skeletons/g1/g1_29dof.xml').getroot()
        # Geometry is unnecessary for FK. Keep every body, inertial, joint and transform unchanged.
        for parent in list(xml.iter()):
            for child in list(parent):
                if child.tag in ('asset','geom'):parent.remove(child)
        model=mujoco.MjModel.from_xml_string(ET.tostring(xml,encoding='unicode'));data=mujoco.MjData(model)
        nodes=skeleton();rng=np.random.default_rng(1234)
        for _ in range(12):
            q=initial_context()[0,0].astype(np.float64);q[7:]=rng.uniform(-.3,.3,29)
            q[3:7]=Rotation.from_rotvec(rng.uniform(-.3,.3,3)).as_quat()[[3,0,1,2]]
            data.qpos[:]=q;mujoco.mj_forward(model,data);positions,rotations=fk(q,nodes)
            for i,node in enumerate(nodes):
                body=model.body(node['name']).id
                np.testing.assert_allclose(positions[i],data.xpos[body],atol=1e-7)
                np.testing.assert_allclose(Rotation.from_quat(rotations[i]).as_matrix(),data.xmat[body].reshape(3,3),atol=1e-7)
    def test_loopback_real_inference_and_reconnect(self):
        request=dict(protocolVersion=1,operation='generate',modelVersion=MODEL_VERSION,requestId=9,
            contextTimestamp=12.5,timeOrigin=100,context=initial_context().flatten().tolist(),mode=0,movement=[0,0,0],facing=[1,0,0])
        results=[]
        for _ in range(2):
            with socket.create_connection(('127.0.0.1',17861),timeout=3) as conn:
                send(conn,request);results.append(receive(conn))
        self.assertEqual(results[0]['requestId'],9);self.assertEqual(results[0]['contextTimestamp'],12.5)
        self.assertEqual(results[0]['qpos'],results[1]['qpos']);self.assertEqual(len(results[0]['qpos']),results[0]['validFrameCount']*36)

if __name__=='__main__':unittest.main()
