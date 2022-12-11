import json
import subprocess
import time
from arena import *
from sys import platform
import os
import signal
import logging
from timer import RepeatedTimer
scene = Scene(host="arena-dev1.conix.io", scene="example")
clientDict = dict()
SceneDict = dict()
healthStatusDict = dict()
remoteRender = dict()


def checkHealthCheck(client, userdata,msg):
    request = json.loads((msg.payload.decode("utf-8")))
    if("data" not in request):
        logging.warning("Request does not have namespace info, ignoring")
        return  
    healthStatusDict[request["data"]] = True
    

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
    remoteRender[data] = renderStatus

    if(data in clientDict and renderStatus == False):
        closeExecutable(data)

    return
def onConnect(client, userdata, msg):
    
    request = json.loads((msg.payload.decode("utf-8")))

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
            openExecutable(data)  
            clientDict[data] = set()
            clientDict[data].add(id)

    return


def openExecutable(data):
    dataSplit = data.split("/")
    nameSpace = dataSplit[0]
    scene = dataSplit[1]
    if(platform == "linux" or platform == "linux2"):
        fileName = ["./arena-hybrid.x86_64",nameSpace,scene]  
    else:
        fileName = ["open","HydribExe.app","--args",nameSpace,scene,scene]
    Popen = subprocess.Popen(fileName)
    SceneDict[data] = Popen    
    
    
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

scene.message_callback_add("realm/g/a/hybrid_rendering/client/connect/#", onConnect)
scene.message_callback_add("realm/g/a/hybrid_rendering/client/disconnect/#", onDisconnect)
scene.message_callback_add("realm/g/a/hybrid_rendering/client/remote/#", renderStatus)
scene.run_tasks()