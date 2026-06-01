using DefaultNamespace;
using System.IO;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif 

public class Fracture : MonoBehaviour, IFracturable
{
    [SerializeField, Tooltip("If not provided, we grab the MeshFilter on this object")] 
    private MeshFilter _meshFilter;
    
    [SerializeField, Tooltip("If not provided, we grab the MeshRenderer on this object")] 
    private MeshRenderer _meshRenderer;
    
    [SerializeField, Tooltip("Will default to the current object, or a parent, if not set specifically")]
    private Rigidbody _rigidbody;

    [SerializeField, Tooltip("If the rigid body contains several fracture objects - what percentage is this one of the whole?")] 
    private float _amountOfParentRigidBody = 1.0f;

    [Tooltip("Fragment Root Parent")]
    public Transform FragmentRootParent;
    
    [Tooltip("Used when generating fragments inside of the editor")]
    public string GeneratedFragmentsAssetName; 
    
    public TriggerOptions triggerOptions;
    public FractureOptions fractureOptions;
    public RefractureOptions refractureOptions;
    public CallbackOptions callbackOptions;
    
    #region IFracturable
    public MeshFilter FractureMeshFilter => _meshFilter;
    public GameObject FractureGameObject => gameObject;
    public Rigidbody Rigidbody => _rigidbody;
    public float PartPercentageOfRigidBody => _amountOfParentRigidBody;
    #endregion

    /// <summary>
    /// The number of times this fragment has been re-fractured.
    /// </summary>
    [HideInInspector]
    public int currentRefractureCount = 0;

    /// <summary>
    /// Collector object that stores the produced fragments
    /// </summary>
    private GameObject fragmentRoot;
    
    [ContextMenu("Print Mesh Info")]
    public void PrintMeshInfo()
    {
        var mesh = FractureMeshFilter.mesh;
        Debug.Log("Positions");

        var positions = mesh.vertices;
        var normals = mesh.normals;
        var uvs = mesh.uv;

        for (int i = 0; i < positions.Length; i++)
        {
            Debug.Log($"Vertex {i}");
            Debug.Log($"POS | X: {positions[i].x} Y: {positions[i].y} Z: {positions[i].z}");
            Debug.Log($"NRM | X: {normals[i].x} Y: {normals[i].y} Z: {normals[i].z} LEN: {normals[i].magnitude}");
            Debug.Log($"UV  | U: {uvs[i].x} V: {uvs[i].y}");
            Debug.Log("");
        }
    }
    
    [ContextMenu("Populate Mesh Settings")]
    public void PopulateMeshSettings()
    {
        _meshFilter = GetComponentInChildren<MeshFilter>();
        _meshRenderer = GetComponentInChildren<MeshRenderer>();
    }

    void Awake()
    {
        if (!_meshFilter)
        {
            _meshFilter = GetComponentInChildren<MeshFilter>();
        }

        if (!_meshRenderer)
        {
            _meshRenderer = GetComponentInChildren<MeshRenderer>();
        }

        if (!_meshFilter || !_meshRenderer)
        {
            Debug.LogWarning($"Fracture component {name} cannot determine meshFilter and/or meshRenderer", gameObject);
        }

        if (!_rigidbody)
        {
            _rigidbody = GetComponent<Rigidbody>();

            if (!_rigidbody)
            {
                _rigidbody = GetComponentInParent<Rigidbody>();
            }
        }
    }

    public void CauseFracture()
    {
        if (Application.isEditor && !Application.isPlaying)
        {
#if UNITY_EDITOR
            EditorDialog.DisplayAlertDialog($"Call CauseFractureInEditor Instead", "You should not call 'CauseFracture' in the editor - call 'CauseFractureInEditor' instead!", "Okay!");
#endif // UNITY_EDITOR
            
            return;
        }

        callbackOptions.CallOnFracture(null, gameObject, transform.position);
        this.ComputeFracture();
    }

    [ContextMenu("Cause Fracture in Editor")]
    public GameObject CauseFractureInEditor()
    {
#if UNITY_EDITOR
        if (!Application.isEditor || Application.isPlaying) return null;

        void EnsureFolderExists(string path)
        {
            // path like "Assets/Foo/Bar/Baz"
            string[] parts = path.Split('/');
            string current = parts[0];
            
            for (int i = 1; i < parts.Length; i++)
            {
                string next = current + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next))
                {
                    AssetDatabase.CreateFolder(current, parts[i]);
                }

                current = next;
            }
        }        
        
        if (!_rigidbody)
        {
            _rigidbody = GetComponent<Rigidbody>();
            
            if (!_rigidbody)
            {
                EditorDialog.DisplayAlertDialog($"Cannot Determine Rigidbody", "Cannot determine rigidbody for fracturing - you must set it manually on the Fracture script.", "Okay!");
                return null;
            }
        }

		if (string.IsNullOrWhiteSpace(GeneratedFragmentsAssetName))
		{
            EditorDialog.DisplayAlertDialog($"No Asset Name Set", "You must provide an asset name used for the file name when generating fragment mesh assets.", "Okay!");
            return null;
		}
        
        string assetPath = $"Assets/Game/AutoGenerated/OpenFracture/{GeneratedFragmentsAssetName}_meshes.asset";		

        if (AssetDatabase.LoadAssetAtPath<Object>(assetPath) != null)
        {
            if (!EditorDialog.DisplayDecisionDialog("Overwrite Existing Mesh Asset?", $"This operation will delete the existing meshes in {assetPath} and replace them!", "Okay", "Cancel"))
            {
                return null;
            }
                
            AssetDatabase.DeleteAsset(assetPath);
        }        
        
        Undo.SetCurrentGroupName($"Fracture {name}");
        int group = Undo.GetCurrentGroup();
        
        if (!FractureMeshFilter)
        {
            PopulateMeshSettings();
        }

		if (this.fragmentRoot)
		{
			foreach(Transform fragment in this.fragmentRoot.transform) 
			{
				DestroyImmediate(fragment.gameObject);
			}
		}

        var previousFragmentRoot = this.fragmentRoot;
        
        Undo.RecordObject(gameObject, "ComputeFracture");
        this.ComputeFracture();

        if (!previousFragmentRoot && fragmentRoot)
        {
            Undo.RegisterCreatedObjectUndo(fragmentRoot, "Created Fracture Root");
        }
 
		if (fragmentRoot)
        {
            EnsureFolderExists(Path.GetDirectoryName(assetPath));
			var meshFilters = fragmentRoot.GetComponentsInChildren<MeshFilter>();

            // Use the first mesh as the "main" asset, add the rest as sub-assets
    		AssetDatabase.CreateAsset(meshFilters[0].sharedMesh, assetPath);
    
		    for (int i = 1; i < meshFilters.Length; i++)
    		{
        		AssetDatabase.AddObjectToAsset(meshFilters[i].sharedMesh, assetPath);
    		}

    		AssetDatabase.SaveAssets();
		}

        Undo.CollapseUndoOperations(group);
        return this.fragmentRoot;
#endif // UNITY_EDITOR
    }

    void OnValidate()
    {
        if (this.transform.parent != null)
        {
            // When an object is fractured, the fragments are created as children of that object's parent.
            // Because of this, they inherit the parent transform. If the parent transform is not scaled
            // the same in all axes, the fragments will not be rendered correctly.
            var scale = this.transform.parent.localScale;
            if (!Mathf.Approximately(scale.x, scale.y) || !Mathf.Approximately(scale.x, scale.z) || !Mathf.Approximately(scale.y, scale.z))
            {
                Debug.LogWarning($"Warning: Parent transform of fractured object must be uniformly scaled in all axes or fragments will not render correctly.", this.transform);
            }
        }
    }

    void OnCollisionEnter(Collision collision)
    {
        if (triggerOptions.triggerType == TriggerType.Collision)
        {
            if (collision.contactCount > 0)
            {
                // Collision force must exceed the minimum force (F = I / T)
                var contact = collision.contacts[0];
                float collisionForce = collision.impulse.magnitude / Time.fixedDeltaTime;

                // Colliding object tag must be in the set of allowed collision tags if filtering by tag is enabled
                bool tagAllowed = triggerOptions.IsTagAllowed(contact.otherCollider.gameObject.tag);

                // Object is unfrozen if the colliding object has the correct tag (if tag filtering is enabled)
                // and the collision force exceeds the minimum collision force.
                if (collisionForce > triggerOptions.minimumCollisionForce && 
                   (!triggerOptions.filterCollisionsByTag || (triggerOptions.filterCollisionsByTag && tagAllowed)))
                {
                    callbackOptions.CallOnFracture(contact.otherCollider, gameObject, contact.point);
                    this.ComputeFracture();
                }
            }
        }
    }

    void OnTriggerEnter(Collider collider)
    {
        if (triggerOptions.triggerType == TriggerType.Trigger)
        {
            // Colliding object tag must be in the set of allowed collision tags if filtering by tag is enabled
            bool tagAllowed = triggerOptions.IsTagAllowed(collider.gameObject.tag);

            if (triggerOptions.filterCollisionsByTag && tagAllowed)
            {
                callbackOptions.CallOnFracture(collider, gameObject, transform.position);
                this.ComputeFracture();
            }
        }
    }

    void Update()
    {
        if (triggerOptions.triggerType == TriggerType.Keyboard)
        {
            if (Input.GetKeyDown(triggerOptions.triggerKey))
            {
                callbackOptions.CallOnFracture(null, gameObject, transform.position);
                this.ComputeFracture();
            }
        }
    }

    /// <summary>
    /// Compute the fracture and create the fragments
    /// </summary>
    /// <returns></returns>
    private void ComputeFracture()
    {
        bool fracturingInEditor = Application.isEditor && !Application.isPlaying;
        var mesh = _meshFilter?.sharedMesh;

        if (mesh != null)
        {
            // If the fragment root object has not yet been created, create it now
            if (this.fragmentRoot == null)
            {
                // Create a game object to contain the fragments
                this.fragmentRoot = new GameObject($"{this.name}Fragments");

                if (FragmentRootParent)
                {
                    this.fragmentRoot.transform.SetParent(FragmentRootParent);
                }
                else
                {
                    this.fragmentRoot.transform.SetParent(this.transform.parent);
                }

                // Each fragment will handle its own scale
                this.fragmentRoot.transform.position = this.transform.position;
                this.fragmentRoot.transform.rotation = this.transform.rotation;
                this.fragmentRoot.transform.localScale = Vector3.one;
            }

            var fragmentTemplate = CreateFragmentTemplate();
			callbackOptions.CallOnTemplateCreated(fragmentTemplate);

            if (fractureOptions.asynchronous && !fracturingInEditor)
            {
                StartCoroutine(Fragmenter.FractureAsync(
                    this,
                    this.fractureOptions,
                    fragmentTemplate,
                    this.fragmentRoot.transform,
                    () =>
                    {
                        // Done with template, destroy it
                        GameObject.Destroy(fragmentTemplate);

                        // Deactivate the original object
                        this.gameObject.SetActive(false);

                        // Fire the completion callback
                        if ((this.currentRefractureCount == 0) ||
                            (this.currentRefractureCount > 0 && this.refractureOptions.invokeCallbacks))
                        {
                            if (callbackOptions.onCompleted != null)
                            {
                                callbackOptions.onCompleted.Invoke();
                            }
                        }
                    }
                ));
            }
            else
            {
                Fragmenter.Fracture(this,
                                    this.fractureOptions,
                                    fragmentTemplate,
                                    this.fragmentRoot.transform);

                // Done with template, destroy it
                if (Application.isPlaying)
                {
                    GameObject.Destroy(fragmentTemplate);
                }
                else
                {
                    GameObject.DestroyImmediate(fragmentTemplate);
                }

                // Deactivate the original object
                this.gameObject.SetActive(false);

                // Fire the completion callback
                if ((this.currentRefractureCount == 0) ||
                    (this.currentRefractureCount > 0 && this.refractureOptions.invokeCallbacks))
                {
                    if (callbackOptions.onCompleted != null)
                    {
                        callbackOptions.onCompleted.Invoke();
                    }
                }
            }
        }
    }

    /// <summary>
    /// Creates a template object which each fragment will derive from
    /// </summary>
    /// <param name="preFracture">True if this object is being pre-fractured. This will freeze all of the fragments.</param>
    /// <returns></returns>
    private GameObject CreateFragmentTemplate()
    {
        // If pre-fracturing, make the fragments children of this object so they can easily be unfrozen later.
        // Otherwise, parent to this object's parent
        GameObject obj = new GameObject();
        obj.name = "Fragment";
        obj.tag = this.tag;

        // Update mesh to the new sliced mesh
        obj.AddComponent<MeshFilter>();

        // Add materials. Normal material goes in slot 1, cut material in slot 2
        var meshRenderer = obj.AddComponent<MeshRenderer>();
        meshRenderer.sharedMaterials = new Material[2] {
            _meshRenderer?.sharedMaterial,
            this.fractureOptions.insideMaterial
        };

        // Copy collider properties to fragment
        var thisCollider = this.GetComponent<Collider>();
        var fragmentCollider = obj.AddComponent<MeshCollider>();
        fragmentCollider.convex = true;
        fragmentCollider.sharedMaterial = thisCollider.sharedMaterial;
        fragmentCollider.isTrigger = thisCollider.isTrigger;

        // Copy rigid body properties to fragment
        var thisRigidBody = _rigidbody;
        var fragmentRigidBody = obj.AddComponent<Rigidbody>();
        fragmentRigidBody.linearVelocity = thisRigidBody.linearVelocity;
        fragmentRigidBody.angularVelocity = thisRigidBody.angularVelocity;
        fragmentRigidBody.linearDamping = thisRigidBody.linearDamping;
        fragmentRigidBody.angularDamping = thisRigidBody.angularDamping;
        fragmentRigidBody.useGravity = thisRigidBody.useGravity;

        // If refracturing is enabled, create a copy of this component and add it to the template fragment object
        if (refractureOptions.enableRefracturing &&
           (this.currentRefractureCount < refractureOptions.maxRefractureCount))
        {
            CopyFractureComponent(obj);
        }

        return obj;
    }

    /// <summary>
    /// Convenience method for copying this component to another component
    /// </summary>
    /// <param name="obj">The GameObject to copy the component to</param>
    private void CopyFractureComponent(GameObject obj)
    {
        var fractureComponent = obj.AddComponent<Fracture>();

        fractureComponent.triggerOptions = this.triggerOptions;
        fractureComponent.fractureOptions = this.fractureOptions;
        fractureComponent.refractureOptions = this.refractureOptions;
        fractureComponent.callbackOptions = this.callbackOptions;
        fractureComponent.currentRefractureCount = this.currentRefractureCount + 1;
        fractureComponent.fragmentRoot = this.fragmentRoot;
    }
}
