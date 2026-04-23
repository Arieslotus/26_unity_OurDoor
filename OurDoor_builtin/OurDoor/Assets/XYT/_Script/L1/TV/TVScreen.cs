using UnityEngine;
using System.Collections;

public class TVScreen : MonoBehaviour
{
    [Header("电视配置")]
    [Tooltip("屏幕的MeshRenderer组件")]
    public MeshRenderer screenMeshRenderer;

    [Tooltip("贴图数组（频道/图片列表）")]
    public Texture[] textureArray;

    [Header("颜色设置")]
    [Tooltip("开机时的屏幕基础色")]
    public Color powerOnColor = Color.white;

    [Tooltip("关机时的屏幕基础色")]
    public Color powerOffColor = Color.black;

    [Header("状态")]
    [SerializeField] private bool isTVOn = false;
    [SerializeField] private int currentTextureIndex = 0;

    // MaterialPropertyBlock 用于独立控制材质属性，不影响其他使用相同材质的物体
    private MaterialPropertyBlock materialPropertyBlock;
    private int colorPropertyID;
    private int mainTexPropertyID;

    // 缓存当前材质，避免重复获取
    private Material currentMaterial;

    void Awake()
    {
        // 初始化 MaterialPropertyBlock
        materialPropertyBlock = new MaterialPropertyBlock();

        // 缓存属性ID以提高性能
        colorPropertyID = Shader.PropertyToID("_Color");
        mainTexPropertyID = Shader.PropertyToID("_MainTex");

        // 检查配置
        if (screenMeshRenderer == null)
        {
            screenMeshRenderer = GetComponent<MeshRenderer>();
            if (screenMeshRenderer == null)
            {
                Debug.LogError($"TVScreen: 物体 {gameObject.name} 上没有 MeshRenderer 组件，请手动配置 screenMeshRenderer！");
            }
        }

        // 验证贴图数组
        if (textureArray == null || textureArray.Length == 0)
        {
            Debug.LogWarning($"TVScreen: 贴图数组为空，请配置贴图！");
        }
    }

    void Start()
    {
        // 初始化屏幕状态（默认关机，黑色）
        if (screenMeshRenderer != null)
        {
            screenMeshRenderer.GetPropertyBlock(materialPropertyBlock);
            materialPropertyBlock.SetColor(colorPropertyID, powerOffColor);
            screenMeshRenderer.SetPropertyBlock(materialPropertyBlock);
        }
    }

    /// <summary>
    /// 开启电视
    /// </summary>
    public void TurnOnTV()
    {
        if (isTVOn)
        {
            Debug.Log("电视已经开启");
            return;
        }

        isTVOn = true;

        if (screenMeshRenderer == null) return;

        screenMeshRenderer.GetPropertyBlock(materialPropertyBlock);

        // 设置基础色为白色
        materialPropertyBlock.SetColor(colorPropertyID, powerOnColor);

        // 如果有贴图，显示当前贴图
        if (textureArray != null && textureArray.Length > 0 && currentTextureIndex < textureArray.Length)
        {
            materialPropertyBlock.SetTexture(mainTexPropertyID, textureArray[currentTextureIndex]);
        }

        screenMeshRenderer.SetPropertyBlock(materialPropertyBlock);

        Debug.Log($"电视已开启，当前频道: {currentTextureIndex}");
    }

    /// <summary>
    /// 关闭电视
    /// </summary>
    public void TurnOffTV()
    {
        if (!isTVOn)
        {
            Debug.Log("电视已经关闭");
            return;
        }

        isTVOn = false;

        if (screenMeshRenderer == null) return;

        screenMeshRenderer.GetPropertyBlock(materialPropertyBlock);

        // 设置基础色为黑色（屏幕变黑）
        materialPropertyBlock.SetColor(colorPropertyID, powerOffColor);

        // 可选：清空贴图或保留（建议保留，但颜色为黑色也看不见）
        // materialPropertyBlock.SetTexture(mainTexPropertyID, null);

        screenMeshRenderer.SetPropertyBlock(materialPropertyBlock);

        Debug.Log("电视已关闭");
    }

    /// <summary>
    /// 切换下一张图片（电视开启时有效）
    /// </summary>
    public void NextPicture()
    {
        if (!isTVOn)
        {
            Debug.Log("电视未开启，无法切换图片");
            return;
        }

        if (textureArray == null || textureArray.Length == 0)
        {
            Debug.LogWarning("没有配置贴图数组");
            return;
        }

        // 切换到下一张（循环）
        currentTextureIndex++;
        if (currentTextureIndex >= textureArray.Length)
        {
            currentTextureIndex = 0;
        }

        // * 
        if(currentTextureIndex ==  textureArray.Length - 1)
        {
            Level1Manager.Instance.SetPassWordFound(); // *
        }

        SetCurrentTexture();

        Debug.Log($"切换到下一张图片: {currentTextureIndex}");
    }

    /// <summary>
    /// 切换上一张图片（电视开启时有效）
    /// </summary>
    public void PreviousPicture()
    {
        if (!isTVOn)
        {
            Debug.Log("电视未开启，无法切换图片");
            return;
        }

        if (textureArray == null || textureArray.Length == 0)
        {
            Debug.LogWarning("没有配置贴图数组");
            return;
        }

        // 切换到上一张（循环）
        currentTextureIndex--;
        if (currentTextureIndex < 0)
        {
            currentTextureIndex = textureArray.Length - 1;
        }

        SetCurrentTexture();

        Debug.Log($"切换到上一张图片: {currentTextureIndex}");
    }

    /// <summary>
    /// 切换到指定索引的图片
    /// </summary>
    /// <param name="index">图片索引</param>
    public void SetPictureByIndex(int index)
    {
        if (!isTVOn)
        {
            Debug.Log("电视未开启，无法切换图片");
            return;
        }

        if (textureArray == null || textureArray.Length == 0)
        {
            Debug.LogWarning("没有配置贴图数组");
            return;
        }

        if (index < 0 || index >= textureArray.Length)
        {
            Debug.LogWarning($"索引 {index} 超出范围 (0-{textureArray.Length - 1})");
            return;
        }

        currentTextureIndex = index;
        SetCurrentTexture();

        Debug.Log($"切换到指定图片: {currentTextureIndex}");
    }

    /// <summary>
    /// 设置当前贴图到材质
    /// </summary>
    private void SetCurrentTexture()
    {
        if (screenMeshRenderer == null) return;
        if (textureArray == null || textureArray.Length == 0) return;
        if (currentTextureIndex >= textureArray.Length) return;

        screenMeshRenderer.GetPropertyBlock(materialPropertyBlock);
        materialPropertyBlock.SetTexture(mainTexPropertyID, textureArray[currentTextureIndex]);
        screenMeshRenderer.SetPropertyBlock(materialPropertyBlock);
    }

    /// <summary>
    /// 获取电视开关状态
    /// </summary>
    public bool IsTVOn
    {
        get { return isTVOn; }
    }

    /// <summary>
    /// 获取当前图片索引
    /// </summary>
    public int CurrentTextureIndex
    {
        get { return currentTextureIndex; }
    }

    /// <summary>
    /// 获取贴图总数
    /// </summary>
    public int TotalTextureCount
    {
        get { return textureArray != null ? textureArray.Length : 0; }
    }
}