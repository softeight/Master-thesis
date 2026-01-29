using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Valve.VR;

public class PointCloudRoot : MonoBehaviour
{

    public Transform pointCloudRoot;
    public SteamVR_Behaviour_Pose controller;
    // Start is called before the first frame update
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        if (controller == null)
        {
            Debug.Log("no controller");
            return;
        }

        Vector3 ctlpos = controller.transform.position;

        float yaxis = controller.transform.eulerAngles.y;
        Quaternion rotY = Quaternion.Euler(0, yaxis, 0);

        //Debug.Log("controller pos:" + controller.transform.position);
        //Debug.Log("controller rot:" + controller.transform.rotation);

        pointCloudRoot.transform.position = new Vector3(ctlpos.x, pointCloudRoot.position.y, ctlpos.z);
        pointCloudRoot.transform.localRotation = rotY;

        //Debug.Log("root pos:" + pointCloudRoot.transform.position);



    }
}
