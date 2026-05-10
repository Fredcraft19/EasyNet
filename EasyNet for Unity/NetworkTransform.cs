using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;
using EasyNet.View;
using System.Text;
using System.Threading.Tasks;
using EasyNet_BackEnd.Data;
using EasyNet;
using NetworkView = EasyNet.View.NetworkView;


class NetworkTransform : MonoBehaviour
{
    public EasyNet.View.NetworkView root;
    [Header("Sync Rules")]
    public bool TrackingPosition;
    public bool TrackingRotation;
    public bool TrackingScale;
    //[Header("Debug Data")]
    private UnityEngine.Vector3 position;
    private Quaternion rotation;
    private UnityEngine.Vector3 scale;

    private void Awake()
    {
        root = GetComponent<NetworkView>();
    }
    void Start()
    {
        StartCoroutine(SlowUpdate());
    }

    IEnumerator SlowUpdate()
    {
        while (true)
        {
            if (root == null || root.Network == null || !root.IsPlayers)
            {
                yield return new WaitForSeconds(1f);
                continue;
            }
            yield return new WaitForSeconds(1f / 40f);

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
        Command updatePosition = new Command(root.Network.client, root.ID, "P", $"{position.x} {position.y} {position.z}");
        updatePosition.Format();
        Packet output = updatePosition.GetPacket();
        root.Network.client.notSentPackets.TryAdd(output.PacketID, output);
        Debug.Log($"Sent My Position: {Bytes.ToString(output.Data)}");
    }
    public void SendRotation()
    {
        Command updatePosition = new Command(root.Network.client, root.ID, "R", $"{rotation.x} {rotation.y} {rotation.z} {rotation.w}");
        updatePosition.Format();
        Packet output = updatePosition.GetPacket();
        root.Network.client.notSentPackets.TryAdd(output.PacketID, output);
    }
    public void SendScale()
    {
        Command updatePosition = new Command(root.Network.client, root.ID, "S", $"{scale.x} {scale.y} {scale.z}");
        updatePosition.Format();
        Packet output = updatePosition.GetPacket();
        root.Network.client.notSentPackets.TryAdd(output.PacketID, output);
    }
}
