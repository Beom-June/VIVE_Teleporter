using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class InteractableUI : MonoBehaviour
{
    private BoxCollider boxCollider;
    private RectTransform rectTransform;
    void Start()
    {
        rectTransform = GetComponent<RectTransform>();
        // Button이라는 UI에 아직 콜라이더를 추가하지 않았기 때문에, AddComponent를 함.
        boxCollider = gameObject.AddComponent<BoxCollider>();
        // 박스 콜라이더 사이즈 설정.
        boxCollider.size = rectTransform.sizeDelta;
    }

    void Update()
    {
        
    }
}
