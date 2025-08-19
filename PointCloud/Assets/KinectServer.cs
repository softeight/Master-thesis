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
    //byte[] data;
    public GameObject pointPrefab;
    public static List<GameObject> Points = new List<GameObject>();
    public int max_points = 5000;

    public short point_max_count = 200;
    byte[] packet_buffer;
    //int count = 0;

    // Start is called before the first frame update
    void Start()
    {
        udpserver = new UdpClient(3363);
        remoteEP = new IPEndPoint(IPAddress.Any, 0);
        packet_buffer = new byte[6 * point_max_count];
        //data = new byte[6];




    }

    // Update is called once per frame
    void Update()
    {

        

        //get the received data according to its format/sequence
        if (udpserver.Available > 0)
        {
            foreach (GameObject obj in Points) 
                Destroy(obj);
            Points.Clear();
            try
            {
                packet_buffer = udpserver.Receive(ref remoteEP);
                if (packet_buffer == null)
                {
                    throw new Exception("...");
                }
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                return;
            }

            for(int i = 0; i < point_max_count; i++)
            {

                short cx = BitConverter.ToInt16(packet_buffer, i*6);
                float x = cx / 1000f; // back to meters
                short cy = BitConverter.ToInt16(packet_buffer, i * 6 + 2);
                float y = cy / 1000f;
                short cz = BitConverter.ToInt16(packet_buffer, i * 6 + 4);
                float z = cz / 1000f;

                Vector3 pos = new Vector3(x, y, z);
                var newPoint = Instantiate(pointPrefab, pos, Quaternion.identity, this.transform);
                Points.Add(newPoint);

            }




           
            //byte r = data[6];
           // byte g = data[7];
            //byte b = data[8];
            //byte a = data[15];
           

           // Vector3 pos = new Vector3(x, y, z);
            //pointPrefab.transform.position = pos;
            
            //var newPoint = Instantiate(pointPrefab,pos,Quaternion.identity);
            //Points.Add(newPoint);

            //if (Points.Count > max_points)
            //{
            //    foreach (var go in Points) Destroy(go);
            //    Points.Clear();
            //}
            //Renderer rend = newPoint.GetComponent<Renderer>();
           // rend.material.color = new Color32(r, g, b, 1);
            //newPoint.GetComponent<Renderer>().material.color = new Color32(r, g, b, 1);


        }
       


    }
}
