using UnityEngine;
using UnityEngine.Rendering;

[ImageEffectAllowedInSceneView]
[ExecuteInEditMode()]
public class NGSS_ContactShadows : MonoBehaviour
{
    [Header("REFERENCES")]
    public Light contactShadowsLight;
    public Shader contactShadowsShader;
    
    [Header("SHADOWS SETTINGS")]
    [Tooltip("Poisson Noise. Randomize samples to remove repeated patterns.")]
    public bool m_noiseFilter = false;

    [Tooltip("Tweak this value to remove soft-shadows leaking around edges.")]
    [Range(0.01f, 1f)]
    public float m_shadowsEdgeTolerance = 0.25f;

    [Tooltip("Overall softness of the shadows.")]
    [Range(0.01f, 1.0f)]
    public float m_shadowsSoftness = 0.25f;

    [Tooltip("Overall distance of the shadows.")]
    [Range(1f, 4.0f)]
    public float m_shadowsDistance = 1f;

    [Tooltip("The distance where shadows start to fade.")]
    [Range(0.1f, 4.0f)]
	public float m_shadowsFade = 1f;

    [Tooltip("Tweak this value if your objects display backface shadows.")]
    [Range(0.0f, 1f)]
    public float m_shadowsFrustumBias = 0.05f;
    /***********************/
    [Header("RAY SETTINGS")]
    [Tooltip("Number of samplers between each step. The higher values produces less gaps between shadows. Keep this value as low as you can!")]
    [Range(16, 128)]
    public int m_raySamples = 64;

    [Tooltip("Samplers scale over distance. Lower this value if you want to speed things up by doing less sampling on far away objects.")]
    [Range(0.0f, 1.0f)]
    public float m_raySamplesScale = 1f;

    [Tooltip("The higher the value, the ticker the shadows will look.")]
    [Range(0.0f, 1.0f)]
	public float m_rayWidth = 0.1f;

    private Texture2D noMixTexture;

    /*******************************************************************************************************************/

    private CommandBuffer blendShadowsCB;
    private CommandBuffer computeShadowsCB;
    private bool isInitialized = false;

    private Camera _mCamera;
    private Camera mCamera
    {
        get
        {
            if (_mCamera == null)
            {
                _mCamera = GetComponent<Camera>();
                if (_mCamera == null) { _mCamera = Camera.main; }
                if (_mCamera == null) { Debug.LogError("NGSS Error: No MainCamera found, please provide one.", this); }
                else { _mCamera.depthTextureMode |= DepthTextureMode.Depth; }
            }
            return _mCamera;
        }
    }

    private Material _mMaterial;
    private Material mMaterial
    {
        get
        {
            if (_mMaterial == null)
            {
                //_mMaterial = new Material(Shader.Find("Hidden/NGSS_ContactShadows"));//Automatic (sometimes it bugs)
                if (contactShadowsShader == null) { Shader.Find("Hidden/NGSS_ContactShadows"); }
                _mMaterial = new Material(contactShadowsShader);//Manual
                if (_mMaterial == null) { Debug.LogWarning("NGSS Warning: can't find NGSS_ContactShadows shader, make sure it's on your project.", this); enabled = false; return null; }
            }
            return _mMaterial;
        }
    }

    void AddCommandBuffers()
    {
        computeShadowsCB = new CommandBuffer { name = "NGSS ContactShadows: Compute" };
        blendShadowsCB = new CommandBuffer { name = "NGSS ContactShadows: Mix" };
        
        if (mCamera)
        {
            bool canAddBuff = true;
            foreach (CommandBuffer cb in mCamera.GetCommandBuffers(mCamera.actualRenderingPath == RenderingPath.Forward ? CameraEvent.AfterDepthTexture : CameraEvent.BeforeLighting)) { if (cb.name == computeShadowsCB.name) { canAddBuff = false; } }
            if (canAddBuff) { mCamera.AddCommandBuffer(mCamera.actualRenderingPath == RenderingPath.Forward ? CameraEvent.AfterDepthTexture : CameraEvent.BeforeLighting, computeShadowsCB); }
        }

        //uncomment me if using the screen space blit | comment me if sampling directly from internal NGSS libraries
        if (contactShadowsLight)
        {
            bool canAddBuff = true;            
            foreach (CommandBuffer cb in contactShadowsLight.GetCommandBuffers(LightEvent.AfterScreenspaceMask)) { if (cb.name == blendShadowsCB.name) { canAddBuff = false; } }
            if (canAddBuff) { contactShadowsLight.AddCommandBuffer(LightEvent.AfterScreenspaceMask, blendShadowsCB); }
        }
    }

    void RemoveCommandBuffers()
	{
        _mMaterial = null;
        if (mCamera) { mCamera.RemoveCommandBuffer(mCamera.actualRenderingPath == RenderingPath.Forward ? CameraEvent.AfterDepthTexture : CameraEvent.BeforeLighting, computeShadowsCB); }
        //We done need this anymore as the contact shadows mix is done directly on shadow internal files
        //if (contactShadowsLight) { contactShadowsLight.RemoveCommandBuffer(LightEvent.AfterScreenspaceMask, blendShadowsCB); }
        isInitialized = false;
    }

	void Init()
	{
        if (isInitialized || contactShadowsLight == null) { return; }

        if (mCamera.actualRenderingPath == RenderingPath.VertexLit)
        {
            Debug.LogWarning("Vertex Lit Rendering Path is not supported by NGSS Contact Shadows. Please set the Rendering Path in your game camera or Graphics Settings to something else than Vertex Lit.", this);
            enabled = false;
            //DestroyImmediate(this);
            return;
        }

        AddCommandBuffers();
        //uncomment me if using the screen space blit | comment me if sampling directly from internal NGSS libraries
        //mix with screen space shadow mask. COMMENT ME IF DIRECTLY CALLED INTERNALLY (Libraries)
        blendShadowsCB.Blit(BuiltinRenderTextureType.CurrentActive, BuiltinRenderTextureType.CurrentActive, mMaterial, 3);

        int cShadow = Shader.PropertyToID("NGSS_ContactShadowRT");
        int cShadow2 = Shader.PropertyToID("NGSS_ContactShadowRT2");
        int dSource = Shader.PropertyToID("NGSS_DepthSourceRT");
        
        computeShadowsCB.GetTemporaryRT(cShadow, -1, -1, 0, FilterMode.Bilinear, RenderTextureFormat.R8);
        computeShadowsCB.GetTemporaryRT(cShadow2, -1, -1, 0, FilterMode.Bilinear, RenderTextureFormat.R8);
        computeShadowsCB.GetTemporaryRT(dSource, -1, -1, 0, FilterMode.Point, RenderTextureFormat.RFloat);
        //computeShadowsCB.SetGlobalTexture(Shader.PropertyToID("ScreenSpaceMask"), BuiltinRenderTextureType.CurrentActive);//requires a commandBuffer on the light, not compatible with local light

        computeShadowsCB.Blit(cShadow, dSource, mMaterial, 0);//clip edges
        computeShadowsCB.Blit(dSource, cShadow, mMaterial, 1);//compute ssrt shadows

        //blur shadows
        computeShadowsCB.SetGlobalVector("ShadowsKernel", new Vector2(0.0f, 1.0f));
        computeShadowsCB.Blit(cShadow, cShadow2, mMaterial, 2);
        computeShadowsCB.SetGlobalVector("ShadowsKernel", new Vector2(1.0f, 0.0f));
        computeShadowsCB.Blit(cShadow2, cShadow, mMaterial, 2);
        computeShadowsCB.SetGlobalVector("ShadowsKernel", new Vector2(0.0f, 2.0f));
        computeShadowsCB.Blit(cShadow, cShadow2, mMaterial, 2);
        computeShadowsCB.SetGlobalVector("ShadowsKernel", new Vector2(2.0f, 0.0f));
        computeShadowsCB.Blit(cShadow2, cShadow, mMaterial, 2);

        computeShadowsCB.SetGlobalTexture("NGSS_ContactShadowsTexture", cShadow);

        //comment me these 3 lines if sampling directly from internal files
        noMixTexture = new Texture2D(1, 1, TextureFormat.ARGB32, false);
        noMixTexture.SetPixel(0, 0, Color.white);
        noMixTexture.Apply();

        isInitialized = true;
	}

    bool IsNotSupported()
    {
#if UNITY_2018_1_OR_NEWER
        return (SystemInfo.graphicsDeviceType == GraphicsDeviceType.OpenGLES2);
#elif UNITY_2017_4_OR_EARLIER
        return (SystemInfo.graphicsDeviceType == GraphicsDeviceType.OpenGLES2 || SystemInfo.graphicsDeviceType == GraphicsDeviceType.PlayStationVita || SystemInfo.graphicsDeviceType == GraphicsDeviceType.N3DS);
#else
        return (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Direct3D9 || SystemInfo.graphicsDeviceType == GraphicsDeviceType.OpenGLES2 || SystemInfo.graphicsDeviceType == GraphicsDeviceType.PlayStationMobile || SystemInfo.graphicsDeviceType == GraphicsDeviceType.PlayStationVita || SystemInfo.graphicsDeviceType == GraphicsDeviceType.N3DS);
#endif
    }

    void OnEnable()
	{
        if (IsNotSupported())
        {
            Debug.LogWarning("Unsupported graphics API, NGSS requires at least SM3.0 or higher and DX9 is not supported.", this);
            this.enabled = false;
            return;
        }

        Init();
    }

    void OnDisable()
    {
        if (isInitialized) { RemoveCommandBuffers(); }
    }

    void OnApplicationQuit()
	{
        if (isInitialized) { RemoveCommandBuffers(); }
	}

    void OnPreRender()
	{
        Init();
        if (isInitialized == false || contactShadowsLight == null) { return; }

        //mMaterial.SetMatrix("InverseProj", Matrix4x4.Inverse(mCamera.projectionMatrix));//proj to cam        
        //mMaterial.SetMatrix("InverseView", mCamera.cameraToWorldMatrix);//cam to world        
        //mMaterial.SetMatrix("InverseViewProj", Matrix4x4.Inverse(GL.GetGPUProjectionMatrix(mCamera.projectionMatrix, false) * mCamera.worldToCameraMatrix));//proj to world
        mMaterial.SetMatrix("WorldToView", mCamera.worldToCameraMatrix);//cam to world        
        mMaterial.SetVector("LightPos", contactShadowsLight.transform.position);//world position
        mMaterial.SetVector("LightDir", -mCamera.transform.InverseTransformDirection(contactShadowsLight.transform.forward));//view space direction
        mMaterial.SetFloat("ShadowsOpacity", 1f - contactShadowsLight.shadowStrength);
        mMaterial.SetFloat("ShadowsEdgeTolerance", m_shadowsEdgeTolerance * 0.075f);
        mMaterial.SetFloat("ShadowsSoftness", m_shadowsSoftness * 4f);
        mMaterial.SetFloat("ShadowsDistance", m_shadowsDistance);        
        mMaterial.SetFloat("ShadowsFade", m_shadowsFade);
        mMaterial.SetFloat("ShadowsBias", m_shadowsFrustumBias * 0.02f);
        mMaterial.SetFloat("RayWidth", m_rayWidth);
        mMaterial.SetFloat("RaySamples", (float)m_raySamples); 
        mMaterial.SetFloat("RaySamplesScale", m_raySamplesScale);
        if (m_noiseFilter) { mMaterial.EnableKeyword("NGSS_CONTACT_SHADOWS_USE_NOISE"); } else { mMaterial.DisableKeyword("NGSS_CONTACT_SHADOWS_USE_NOISE"); }
        if (contactShadowsLight.type != LightType.Directional) { mMaterial.EnableKeyword("NGSS_USE_LOCAL_CONTACT_SHADOWS"); } else { mMaterial.DisableKeyword("NGSS_USE_LOCAL_CONTACT_SHADOWS"); }
    }
    //uncomment me if using the screen space blit | comment me if sampling directly from internal NGSS libraries
    void OnPostRender()
    {
        Shader.SetGlobalTexture("NGSS_ContactShadowsTexture", noMixTexture);
    }
}
