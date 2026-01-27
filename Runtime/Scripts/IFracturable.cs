using UnityEngine;

namespace DefaultNamespace
{
    public interface IFracturable
    {
        public MeshFilter FractureMeshFilter { get; }
        public GameObject FractureGameObject { get; }
        
        public Rigidbody Rigidbody { get; }
        public float PartPercentageOfRigidBody => 1.0f;
        public GameObject FragmentParentObject => null;
    }
}