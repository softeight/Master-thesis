using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Varjo.XR;

public class XRController : MonoBehaviour
{
    // Start is called before the first frame update
    void Start()
    {
        VarjoMixedReality.StartRender();
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    private void OnDestroy()
    {

        VarjoMixedReality.StopRender();
    }
}
