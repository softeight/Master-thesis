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

        // Get controller transform
        Vector3 ctlpos = controller.transform.position;

        // Get controller rotation (for y axle)
        float yaxis = controller.transform.eulerAngles.y;
        Quaternion rotY = Quaternion.Euler(0, yaxis, 0);

        // Transform controller's position and rotation to the Root Object
        pointCloudRoot.transform.position = new Vector3(ctlpos.x, pointCloudRoot.position.y, ctlpos.z);
        pointCloudRoot.transform.localRotation = rotY;





    }
}
