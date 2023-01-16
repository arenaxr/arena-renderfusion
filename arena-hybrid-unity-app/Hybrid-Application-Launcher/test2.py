from arena import *

def testCallBack(client, userdata,msg):
    pass
scene = Scene(host="arena-dev1.conix.io", scene="example")

scene.message_callback_add("realm/g/a/hybrid_rendering/client/hi/#", testCallBack)