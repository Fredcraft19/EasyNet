using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;
using EasyNet.Behaviour;
using System.Text;
using System.Threading.Tasks;
using EasyNet_BackEnd.Data;
using EasyNet.Behaviour;
using EasyNet;


class NetworkTransform : MonoBehaviour
{
    public NetworkBehaviour root;

    [Header("Sync Rules")]
    public bool TrackingPosition;
    public bool TrackingRotation;
    public bool TrackingScale;
    [Header("Data")]
    public UnityEngine.Vector3 position;
    public Quaternion rotation;
    public UnityEngine.Vector3 scale;

    private void Awake()
    {
        root = GetComponent<NetworkBehaviour>();
    }
    void Start()
    {
        StartCoroutine(SlowUpdate());
    }

    IEnumerator SlowUpdate()
    {
        while (true)
        {
            if (root == null || root.root == null || !root.isPlayers)
            {
                yield return new WaitForSeconds(1f);
                continue;
            }
            yield return new WaitForSeconds(1f / (float)root.root.tickSpeed);

            if (position != transform.position)
            {
                position = transform.position;
                if (TrackingPosition)
                    SendPosition();
            }
            if (rotation != transform.rotation)
            {
                rotation = transform.rotation;
                if (TrackingRotation)
                    SendRotation();
            }
            if (scale != transform.localScale)
            {
                scale = transform.localScale;
                if (TrackingScale)
                    SendScale();
            }
        }
    }


    public void SendPosition()
    {
        Command updatePosition = new Command(root.root.client, root.ID, "P", $"{position.x} {position.y} {position.z}");
        updatePosition.Format();
        Packet output = updatePosition.GetPacket();
        root.root.client.notSentPackets.TryAdd(output.PacketID, output);
        Debug.Log($"Sent My Position: {Bytes.ToString(output.Data)}");
    }
    public void SendRotation()
    {
        Command updatePosition = new Command(root.root.client, root.ID, "R", $"{rotation.x} {rotation.y} {rotation.z} {rotation.w}");
        updatePosition.Format();
        Packet output = updatePosition.GetPacket();
        root.root.client.notSentPackets.TryAdd(output.PacketID, output);
    }
    public void SendScale()
    {
        Command updatePosition = new Command(root.root.client, root.ID, "S", $"{scale.x} {scale.y} {scale.z}");
        updatePosition.Format();
        Packet output = updatePosition.GetPacket();
        root.root.client.notSentPackets.TryAdd(output.PacketID, output);
    }
}
