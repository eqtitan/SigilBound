// Closes a high-detail hero's weapon hand around the grip after every animation frame (phase F):
// the free UAL clips leave the fingers open, so a held weapon floated in an open hand. Same rule as
// blenderforge/ubc.py grip_pose: each finger joint turns about the grip axis (the knuckle line, index
// to little finger) toward the palm, from its rest pose. Runs in LateUpdate, after the Animation.
using System.Collections.Generic;
using UnityEngine;

namespace Sigilbound.Client
{
    public sealed class HandGrip : MonoBehaviour
    {
        public string Side = "r";
        static readonly string[] Fingers = { "index", "middle", "ring", "pinky", "thumb" };
        static readonly string[] Joints = { "01", "02", "03" };
        static readonly float[] Curl = { 62f, 78f, 55f };
        Transform _hand, _index, _pinky, _thumb;
        readonly List<(Transform t, Quaternion rest, float deg)> _bones = new List<(Transform, Quaternion, float)>();

        void Start()
        {
            _hand = BodyShaper.Find(transform, "hand_" + Side);
            _index = BodyShaper.Find(transform, "index_01_" + Side);
            _pinky = BodyShaper.Find(transform, "pinky_01_" + Side);
            _thumb = BodyShaper.Find(transform, "thumb_02_" + Side);
            foreach (var f in Fingers)
                for (int j = 0; j < 3; j++)
                {
                    var t = BodyShaper.Find(transform, $"{f}_{Joints[j]}_{Side}");
                    if (t != null) _bones.Add((t, t.localRotation, Curl[j] * (f == "thumb" ? 0.6f : 1f)));
                }
            if (_hand == null || _index == null || _pinky == null || _thumb == null) enabled = false;
        }

        void LateUpdate()
        {
            Vector3 axis = (_index.position - _pinky.position).normalized;
            Vector3 along = ((_index.position + _pinky.position) * 0.5f - _hand.position).normalized;
            Vector3 palm = Vector3.Cross(axis, along).normalized;
            if (Vector3.Dot(palm, _thumb.position - _hand.position) < 0) palm = -palm;
            foreach (var (t, rest, deg) in _bones)
            {
                t.localRotation = rest;   // start from the rest pose (the clips' fingers are open)
                Vector3 child = t.childCount > 0 ? t.GetChild(0).position : t.position + t.up * 0.03f;
                Vector3 v = child - t.position;
                // Turn toward the palm: pick the sign that moves the next joint onto the palm side.
                float s = Vector3.Dot(Quaternion.AngleAxis(deg, axis) * v, palm) >= Vector3.Dot(Quaternion.AngleAxis(-deg, axis) * v, palm) ? 1f : -1f;
                t.rotation = Quaternion.AngleAxis(deg * s, axis) * t.rotation;
            }
        }
    }
}
