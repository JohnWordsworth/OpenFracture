using UnityEngine;

namespace DefaultNamespace
{
    public interface IFracturable
    {
        public MeshFilter FractureMeshFilter { get; }
        public GameObject FractureGameObject { get; }
    }
}