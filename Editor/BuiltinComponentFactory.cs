using UnityEngine;
using System;

namespace Antimatter
{
    public static class BuiltinComponentFactory
    {
        public static Type ResolveType(string name)
        {
            return name switch
            {
                "Rigidbody" => typeof(Rigidbody),
                "BoxCollider" => typeof(BoxCollider),
                "SphereCollider" => typeof(SphereCollider),
                "CapsuleCollider" => typeof(CapsuleCollider),
                "MeshCollider" => typeof(MeshCollider),
                "CharacterController" => typeof(CharacterController),
                "AudioSource" => typeof(AudioSource),
                "Animator" => typeof(Animator),
                "NavMeshAgent" => typeof(UnityEngine.AI.NavMeshAgent),
                "NavMeshObstacle" => typeof(UnityEngine.AI.NavMeshObstacle),
                "LineRenderer" => typeof(LineRenderer),
                "TrailRenderer" => typeof(TrailRenderer),
                "Light" => typeof(Light),
                "Camera" => typeof(Camera),
                "FixedJoint" => typeof(FixedJoint),
                "HingeJoint" => typeof(HingeJoint),
                "SpringJoint" => typeof(SpringJoint),
                "ParticleSystem" => typeof(ParticleSystem),
                _ => null
            };
        }

        public static Component Add(GameObject go, string name)
        {
            return name switch
            {
                "Rigidbody" => go.AddComponent<Rigidbody>(),
                "BoxCollider" => go.AddComponent<BoxCollider>(),
                "SphereCollider" => go.AddComponent<SphereCollider>(),
                "CapsuleCollider" => go.AddComponent<CapsuleCollider>(),
                "MeshCollider" => go.AddComponent<MeshCollider>(),
                "CharacterController" => go.AddComponent<CharacterController>(),
                "AudioSource" => go.AddComponent<AudioSource>(),
                "Animator" => go.AddComponent<Animator>(),
                "NavMeshAgent" => go.AddComponent<UnityEngine.AI.NavMeshAgent>(),
                "NavMeshObstacle" => go.AddComponent<UnityEngine.AI.NavMeshObstacle>(),
                "LineRenderer" => go.AddComponent<LineRenderer>(),
                "TrailRenderer" => go.AddComponent<TrailRenderer>(),
                "Light" => go.AddComponent<Light>(),
                "Camera" => go.AddComponent<Camera>(),
                "FixedJoint" => go.AddComponent<FixedJoint>(),
                "HingeJoint" => go.AddComponent<HingeJoint>(),
                "SpringJoint" => go.AddComponent<SpringJoint>(),
                "ParticleSystem" => go.AddComponent<ParticleSystem>(),
                _ => null
            };
        }
    }
}
