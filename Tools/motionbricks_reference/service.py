"""One bounded request at a time, loopback-only, length-prefixed UTF-8 JSON v1."""
import argparse
import json
import socket
import struct
import time
import os
import numpy as np
from reference import Planner, MODEL_VERSION, FPS, inputs

MAX_BYTES=256*1024

def read_exact(conn,n):
    data=bytearray()
    while len(data)<n:
        chunk=conn.recv(n-len(data))
        if not chunk: raise EOFError('Disconnected')
        data.extend(chunk)
    return data

def receive(conn):
    n=struct.unpack('<I',read_exact(conn,4))[0]
    if not 0<n<=MAX_BYTES: raise ValueError('Message length out of bounds')
    return json.loads(read_exact(conn,n),parse_constant=lambda x: (_ for _ in ()).throw(ValueError(x)))

def send(conn,data):
    raw=json.dumps(data,allow_nan=False,separators=(',',':')).encode()
    if len(raw)>MAX_BYTES: raise ValueError('Response too large')
    conn.sendall(struct.pack('<I',len(raw))+raw)

def handle(planner,r):
    if r.get('protocolVersion')!=1: raise ValueError('Protocol version must be 1')
    base=dict(protocolVersion=1,modelVersion=MODEL_VERSION,frameRate=FPS,provider='Python ONNX Runtime CPU',targetPoseSupported=False)
    if r.get('operation')=='ready': return dict(base,ready=True)
    if r.get('operation')!='generate': raise ValueError('Unknown operation')
    context=np.asarray(r['context'],np.float32)
    if context.size!=144 or not np.isfinite(context).all(): raise ValueError('Expected finite 4x36 context')
    if np.max(abs(np.linalg.norm(context.reshape(4,36)[:,3:7],axis=-1)-1))>.01: raise ValueError('Invalid root quaternion')
    mode=int(r['mode'])
    if mode not in (0,1,2,3): raise ValueError('Only validated locomotion modes 0..3 exposed')
    for key in ('movement','facing'):
        a=np.asarray(r[key],np.float32)
        if a.shape!=(3,) or not np.isfinite(a).all(): raise ValueError('Invalid direction')
    for key in ('contextTimestamp','timeOrigin'):
        if not np.isfinite(r[key]): raise ValueError('Invalid timestamp')
    if r['modelVersion']!=MODEL_VERSION: raise ValueError('Model revision mismatch')
    q,ms=planner.generate(inputs(context,mode,r['movement'],r['facing'],int(r.get('seed',1234))))
    return dict(base,requestId=r['requestId'],contextTimestamp=r['contextTimestamp'],timeOrigin=r['timeOrigin'],
                validFrameCount=len(q),qpos=q.reshape(-1).tolist(),inferenceMs=ms)

def main():
    p=argparse.ArgumentParser(); p.add_argument('--port',type=int,default=17861); args=p.parse_args()
    planner=Planner()
    with socket.socket() as listener:
        # Exclusive bind prevents accidentally starting a second owner on Windows.
        if hasattr(socket,'SO_EXCLUSIVEADDRUSE'): listener.setsockopt(socket.SOL_SOCKET,socket.SO_EXCLUSIVEADDRUSE,1)
        listener.bind(('127.0.0.1',args.port)); listener.listen(1); listener.settimeout(1)
        print(json.dumps(dict(ready=True,port=args.port,processId=os.getpid(),modelVersion=MODEL_VERSION,loadSeconds=planner.load_seconds)),flush=True)
        while True:
            try: conn,_=listener.accept()
            except socket.timeout: continue
            with conn:
                conn.settimeout(3)
                try: send(conn,handle(planner,receive(conn)))
                except (ValueError,KeyError,EOFError,OSError,OverflowError) as exc:
                    try: send(conn,dict(protocolVersion=1,error=str(exc)))
                    except OSError: pass

if __name__=='__main__': main()
