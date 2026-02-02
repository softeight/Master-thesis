using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem.XR;
using Valve.VR;

public class GetControllerPose : MonoBehaviour
{
    public SteamVR_Behaviour_Pose controller;
    // Start is called before the first frame update
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        if (controller == null) return;

        Vector3 pos = controller.transform.position;
        Quaternion rot = controller.transform.rotation;

        Debug.Log("controller pos:" + controller.transform.position);
        Debug.Log("controller rot:" + controller.transform.rotation);

        transform.position = pos;
        transform.rotation = rot;
    }
}
