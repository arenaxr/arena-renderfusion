import json
import logging
import subprocess
import uuid
from sys import platform

from arena import *

scene = Scene(host="arena-dev1.conix.io", scene="example")
# TODO (mwfarb): update to new scene-scoped render fusion topic-v5 structure
CLIENT_CONNECT = "realm/g/a/hybrid_rendering/client/connect/#"
CLIENT_DISCONNECT = "realm/g/a/hybrid_rendering/client/disconnect/#"
CLIENT_REMOTE = "realm/g/a/hybrid_rendering/client/remote/#"
SERVER_HEALTH = "realm/g/a/hybrid_rendering/server/health/#"
HAL_CONNECT = "realm/g/a/hybrid_rendering/HAL/connect/"

clientDict = dict()
SceneDict = dict()
remoteRender = dict()
executableQueue = []

def openExecutable(data):
    if (platform == "linux" or platform == "linux2"):
        fileName = ["./arena-renderfusion.x86_64",data]
    else:
        fileName = ["open","RenderFusionExe.app","--args",data]
    Popen = subprocess.Popen(fileName)
    SceneDict[data] = Popen

def setupApp():
    id = str(uuid.uuid1())
    executableQueue.append(id)
    openExecutable(id)

def onDisconnect(client, userdata,msg):
    request = json.loads((msg.payload.decode("utf-8")))
    print(request)
    if ("data" not in request):
        logging.warning("Request does not have namespace info, ignoring")
        return
    data = request['data']
    id = request["id"]
    if (data in clientDict):
        if (id in clientDict[data]):
            clientDict[data].remove(id)

        if (not clientDict[data]):
            closeExecutable(data)

def renderStatus(client, userdata, msg):
    request = json.loads((msg.payload.decode("utf-8")))
    print(request)
    if ("data" not in request and "remoteRendered" not in request["data"]):
        logging.warning("Request does not have namespace info, ignoring")
        return
    renderStatus= request["data"]["remoteRendered"]
    data = request["data"]["data"]
    if (data not in remoteRender):
        remoteRender[data] = set()
        remoteRender[data].add(msg)
    else:
        remoteRender[data].add(msg)

def sendRenderStatus(client, userdata,msg):
    request = json.loads((msg.payload.decode("utf-8")))
    print(request)

def onConnect(client, userdata, msg):
    request = json.loads((msg.payload.decode("utf-8")))
    request['type'] = "HAL"
    #print(request)
    if ("data" not in request):
        logging.warning("Request does not have namespace info, ignoring")
        return
    connectData = request['data']
    print(connectData)
    data = connectData['namespacedScene']


    if ("namespacedScene"not in connectData):
        logging.warning("Request does not have namespace info, ignoring")
        return
    data = connectData['namespacedScene']
    id = request["id"]
    if (data in clientDict):
        clientDict[data].add(id)
    else:
        topic = HAL_CONNECT + executableQueue.pop()
        print(topic)
        scene.mqttc.publish(topic,json.dumps(request, ensure_ascii=False).encode('utf-8'))
        clientDict[data] = set()
        clientDict[data].add(id)

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

setupApp()

scene.message_callback_add(CLIENT_CONNECT, onConnect)
scene.message_callback_add(CLIENT_DISCONNECT, onDisconnect)
scene.message_callback_add(CLIENT_REMOTE, renderStatus)
scene.message_callback_add(SERVER_HEALTH, sendRenderStatus)
scene.run_tasks()
