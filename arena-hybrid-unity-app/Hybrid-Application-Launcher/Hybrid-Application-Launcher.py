import json
import subprocess
from arena import *
from sys import platform
import logging
import uuid

scene = Scene(host="arena-dev1.conix.io", scene="example")
clientDict = dict()
SceneDict = dict()
healthStatusDict = dict()
remoteRender = dict()
executableQueue = []
def openExecutable(data):
   
    if(platform == "linux" or platform == "linux2"):
        fileName = ["./arena-hybrid.x86_64",data]  
    else:
        fileName = ["open","HydribExe.app","--args",data]
    Popen = subprocess.Popen(fileName)
    SceneDict[data] = Popen    

def setupHybrid():
    id = str(uuid.uuid1())
    executableQueue.append(id)
    openExecutable(id)
    return

def onDisconnect(client, userdata,msg):
    request = json.loads((msg.payload.decode("utf-8")))
    print(request)
    if("data" not in request):
        logging.warning("Request does not have namespace info, ignoring")
        return    
    data = request['data']
    id = request["id"]
    if(data in clientDict):
        if(id in clientDict[data]):
            clientDict[data].remove(id)

        if(not clientDict[data]):
            closeExecutable(data)

def renderStatus(client, userdata, msg):
    request = json.loads((msg.payload.decode("utf-8")))
    print(request)
    if("data" not in request and "remoteRendered" not in request["data"]):
        logging.warning("Request does not have namespace info, ignoring")
        return
    renderStatus= request["data"]["remoteRendered"]
    data = request["data"]["data"]
    if(data not in remoteRender):
        remoteRender[data] = set()
        remoteRender[data].add(msg)
    else:
        remoteRender[data].add(msg)

    return
def sendRenderStatus(client, userdata,msg):
    request = json.loads((msg.payload.decode("utf-8")))
    print(request)
    return 
def onConnect(client, userdata, msg):
    
    request = json.loads((msg.payload.decode("utf-8")))
    request['type'] = "HAL"
    #print(request)
    if("data" not in request):
        logging.warning("Request does not have namespace info, ignoring")
        return
    connectData = request['data']
    print(connectData)
    data = connectData['namespacedScene']
    

    if("namespacedScene"not in connectData):
        logging.warning("Request does not have namespace info, ignoring")
        return
    data = connectData['namespacedScene']
    id = request["id"]
    if(data in clientDict):
        clientDict[data].add(id)
    else:
            topic = "realm/g/a/hybrid_rendering/HAL/connect/" + executableQueue.pop()
            print(topic)
            scene.mqttc.publish(topic,json.dumps(request, ensure_ascii=False).encode('utf-8'))
            clientDict[data] = set()
            clientDict[data].add(id)

    return



    
    
def closeExecutable(data):
    print(data)
    Popen = SceneDict[data]
    
    pid = Popen.pid
    try:
        Popen.kill()
    except:
        logging.warning("Process Does not exist")
    SceneDict.pop(data)
    clientDict.pop(data)
    pass

setupHybrid()

scene.message_callback_add("realm/g/a/hybrid_rendering/client/connect/#", onConnect)
scene.message_callback_add("realm/g/a/hybrid_rendering/client/disconnect/#", onDisconnect)
scene.message_callback_add("realm/g/a/hybrid_rendering/client/remote/#", renderStatus)
scene.message_callback_add("realm/g/a/hybrid_rendering/server/health/#", sendRenderStatus)
scene.run_tasks()