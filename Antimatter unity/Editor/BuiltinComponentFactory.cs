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
                "CharacterController" => typeof(CharacterController),
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
                "CharacterController" => go.AddComponent<CharacterController>(),
                _ => null
            };
        }
    }
}
