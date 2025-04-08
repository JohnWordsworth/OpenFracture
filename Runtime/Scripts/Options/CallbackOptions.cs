using System;
using UnityEngine;
using UnityEngine.Events;

[Serializable]
public class CallbackOptions
{
    [Tooltip("This callback is invoked when a fracture has been triggered. Not called for slicing and prefracturing.")]
    public UnityEvent<Collider, GameObject, Vector3> onFracture;
    
    [Tooltip("This callback is invoked when a fracture effect generates the internal template which is used as a basis for each created game object.")]
    public UnityEvent<GameObject> onTemplateCreated;
    

    [Tooltip("This callback is invoked when the fracturing/slicing process has been completed.")]
    public UnityEvent onCompleted;


    public CallbackOptions()
    {
        this.onCompleted = null;
    }

    public void CallOnFracture(Collider instigator, GameObject fracturedObject, Vector3 point)
    {
        onFracture?.Invoke(instigator, fracturedObject, point);
    }

    public void CallOnTemplateCreated(GameObject templateObject)
    {
        onTemplateCreated?.Invoke(templateObject);
    }
}