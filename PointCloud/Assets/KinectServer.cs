using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System;
using System.Collections.Specialized;

public class KinectServer : MonoBehaviour
{
    UdpClient udpserver;
    IPEndPoint remoteEP;
    byte[] data;
    public GameObject pointPrefab;


    // Start is called before the first frame update
    void Start()
    {
        udpserver = new UdpClient(3363);
        remoteEP = new IPEndPoint(IPAddress.Any, 0);
        data = new byte[9];

        

}

    // Update is called once per frame
    void Update()
    {

        //get the received data according to its format/sequence
        if(udpserver.Available > 0)
        {
            try
            {
                data = udpserver.Receive(ref remoteEP);
                if (data == null)
                {                   
                    return;
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
            }

            short cx = BitConverter.ToInt16(data, 0);
            float x = cx / 1000f; // back to meters
            short cy = BitConverter.ToInt16(data, 2);
            float y = cy / 1000f;
            short cz = BitConverter.ToInt16(data, 4);
            float z = cz / 1000f;
            byte r = data[6];
            byte g = data[7];
            byte b = data[8];
            //byte a = data[15];
           

            Vector3 pos = new Vector3(x, y, z);
            //pointPrefab.transform.position = pos;
            
            var newPoint = Instantiate(pointPrefab,pos,Quaternion.identity);
            Renderer rend = newPoint.GetComponent<Renderer>();
            rend.material.color = new Color32(r, g, b, 1);
            //newPoint.GetComponent<Renderer>().material.color = new Color32(r, g, b, 1);


        }
       


    }
}
