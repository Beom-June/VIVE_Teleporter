using UnityEngine;
using Valve.VR;

[AddComponentMenu("Vive Teleporter/Vive Teleporter")]
[RequireComponent(typeof(Camera), typeof(BorderRenderer))]
public class TeleportVive : MonoBehaviour {
    [Tooltip("Parabolic Pointer object to pull destination points from, and to assign to each controller.")]
    public ParabolicPointer Pointer;
    // SteamVR tracking space 넣는 변수
    [Tooltip("Origin of the SteamVR tracking space")]
    public Transform OriginTransform;
    // Player 헤드 넣는 변수
    [Tooltip("Transform of the player's head")]
    public Transform HeadTransform;
    
    // fade-in/fade-out 애니메이션에 소요되는 시간.
    [Tooltip("Duration of the \"blink\" animation (fading in and out upon teleport) in seconds.")]
    public float TeleportFadeDuration = 0.2f;
    // Controller가 클릭시 응답해야하는 빈도 측정.  작은 값 = 빠르게 누른 것.
    [Tooltip("The player feels a haptic pulse in the controller when they raise / lower the controller by this many degrees.  Lower value = faster pulses.")]
    public float HapticClickAngleStep = 10;

    // 텔레포트로 이동할 위치를 선택할 때,  경계를 렌터링하는 테두리 렌더러. 
    private BorderRenderer RoomBorder;

    // 텔레포트 영역안에서 fade in/out 되는 애니메이터. 여기에 True이면,  boolean parameter "Enabled" 가 있어야함.
    // 선택 가능한 영역이 지면에 표시
    [SerializeField]
    [Tooltip("Animator with a boolean \"Enabled\" parameter that is set to true when the player is choosing a place to teleport.")]
    private Animator NavmeshAnimator;
    private int EnabledAnimatorID;

    // fade in/fade out 하는 렌더링 하는 데 사용되는 Material.
    [Tooltip("Material used to render the fade in/fade out quad.")]
    [SerializeField]
    private Material FadeMaterial;
    private Material FadeMaterialInstance;
    private int MaterialFadeID;

    // SteamVR controllers 를 풀링함.
    [Tooltip("Array of SteamVR controllers that may used to select a teleport destination.")]
    public SteamVR_Behaviour_Pose[] Controllers;
    private SteamVR_Behaviour_Pose ActiveController;

    // 텔레포트 변수
    public SteamVR_Action_Boolean teleport;
    // 햅틱 변수
    public SteamVR_Action_Vibration haptic;

    // 텔레포트의 현재 사용을 나타냄.
    // None : 플레이어가 현재 텔레포트를 사용하지 않음.
    // Selecting: 플레이어가 현재 텔레포트 대상을 선택하고 있음. (터치패드를 누르고 있음.)
    // Teleporting: 플레이어가 텔레포트를 선택했으며 텔레포트 중. (fading in/out)
    public TeleportState CurrentTeleportState { get; private set; }

    private Vector3 LastClickAngle = Vector3.zero;
    private bool IsClicking = false;

    private bool FadingIn = false;
    private float TeleportTimeMarker = -1;

    private Mesh PlaneMesh;

    void Start()
    {
        // 사용자가 터치패드를 누를 때 까지 포인터 그래픽 사용안함.
        Pointer.enabled = false;

        // 플레이어가 텔레포트로 이동하지 않도록 표시
        CurrentTeleportState = TeleportState.None;

        // 텔레포트 시 Fade Out 그래픽에 사용되는 표준 평면 메시
        // 이렇게하면, 인스펙터에 단순한 평면메시를 제공할 필요가 없음
        PlaneMesh = new Mesh();
        Vector3[] verts = new Vector3[]
        {
            new Vector3(-1, -1, 0),
            new Vector3(-1, 1, 0),
            new Vector3(1, 1, 0),
            new Vector3(1, -1, 0)
        };

        int[] elts = new int[] { 0, 1, 2, 0, 2, 3 };
        PlaneMesh.vertices = verts;
        PlaneMesh.triangles = elts;
        PlaneMesh.RecalculateBounds();

        if(FadeMaterial != null)
        {
            FadeMaterialInstance = new Material(FadeMaterial);
        }
        // 몇 가지 표준 변수 설정
        MaterialFadeID = Shader.PropertyToID("_Fade");
        EnabledAnimatorID = Animator.StringToHash("Enabled");

        RoomBorder = GetComponent<BorderRenderer>();

        Vector3 p0, p1, p2, p3;
        if (GetChaperoneBounds(out p0, out p1, out p2, out p3))
        {
            // 카메라 rig 회전에 맞게 회전
            var originRotationMatrix = Matrix4x4.TRS(Vector3.zero, OriginTransform.rotation, Vector3.one);

            BorderPointSet p = new BorderPointSet(new Vector3[] {
                originRotationMatrix * p0,
                originRotationMatrix * p1,
                originRotationMatrix * p2,
                originRotationMatrix * p3,
                originRotationMatrix * p0,
            });
            RoomBorder.Points = new BorderPointSet[]
            {
                p
            };
        }

        RoomBorder.enabled = false;
    }

    // SteamVR 플레이어 영역의 보호자 경계를 요청. 아직 수행하지 않은 경우에는 작동하지 않음.
    // 룸 설정.
    // 파라미터 값 p0, p1, p2, p3 보호자 경계를 구성하는 포인트
    // 리턴 영역 검색이 성공적이었는지 여부를 반환
    public static bool GetChaperoneBounds(out Vector3 p0, out Vector3 p1, out Vector3 p2, out Vector3 p3)
    {
        var initOpenVR = (!SteamVR.active && !SteamVR.usingNativeSupport);
        if (initOpenVR)
        {
            var error = EVRInitError.None;
            OpenVR.Init(ref error, EVRApplicationType.VRApplication_Other);
        }

        var chaperone = OpenVR.Chaperone;
        HmdQuad_t rect = new HmdQuad_t();
        bool success = (chaperone != null) && chaperone.GetPlayAreaRect(ref rect);

        p0 = new Vector3(rect.vCorners0.v0, rect.vCorners0.v1, rect.vCorners0.v2);
        p1 = new Vector3(rect.vCorners1.v0, rect.vCorners1.v1, rect.vCorners1.v2);
        p2 = new Vector3(rect.vCorners2.v0, rect.vCorners2.v1, rect.vCorners2.v2);
        p3 = new Vector3(rect.vCorners3.v0, rect.vCorners3.v1, rect.vCorners3.v2);

        if (success == false)
        {
            Debug.LogWarning("Failed to get Calibrated Play Area bounds!  Make sure you have tracking first, and that your space is calibrated.");
        }

        if (initOpenVR)
        {
            OpenVR.Shutdown();
        }
        return success;
    }

    void OnPostRender()
    {
        if(CurrentTeleportState == TeleportState.Teleporting)
        {
            // 텔레포트할 때 Fade in/out 애니메이션 수행.
            // in/out, 그리고 완전 검정색일 때 사용자가 텔레포트함.
            float alpha = Mathf.Clamp01((Time.time - TeleportTimeMarker) / (TeleportFadeDuration / 2));
            if (FadingIn)
            {
                alpha = 1 - alpha;
            }
            Matrix4x4 local = Matrix4x4.TRS(Vector3.forward * 0.3f, Quaternion.identity, Vector3.one);
            FadeMaterialInstance.SetPass(0);
            FadeMaterialInstance.SetFloat(MaterialFadeID, alpha);
            Graphics.DrawMeshNow(PlaneMesh, transform.localToWorldMatrix * local);
        }
    }

	void Update ()
    {
        // 현재 텔레포트중이면
        if(CurrentTeleportState == TeleportState.Teleporting)
        {
            // 텔레포트 시간의 절반이 경과할 때까지 기다린 후 다음 이벤트를 수행
            // Fade out에서 Fade in으로 전환. Fade in에서 정지까지의 스위치 모두 Fade시간의 절반.
            if(Time.time - TeleportTimeMarker >= TeleportFadeDuration / 2)
            {
                if(FadingIn)
                {
                    // Fade in 끝냄
                    CurrentTeleportState = TeleportState.None;
                } 
                else
                {
                    // Fade out 끝냄 그리고 텔레포트 이동함.
                    Vector3 offset = OriginTransform.position - HeadTransform.position;
                    offset.y = 0;
                    OriginTransform.position = Pointer.SelectedPoint + offset;
                }
                TeleportTimeMarker = Time.time;
                FadingIn = !FadingIn;
            }
        }
        // 이 시점에서는 텔레포트 사용 X. 컨트롤러 입력에대해 알 수 있는 부분.
        else if(CurrentTeleportState == TeleportState.Selecting)
        {
            Debug.Assert(ActiveController != null);

            // 활성 컨트롤러. (사용자가 트랙 패드를 누르고 있음
            // 관련 버튼 데이터에 대한 폴링 컨트롤러
            bool shouldTeleport = teleport.GetStateUp(SteamVR_Input_Sources.Any);

            if (shouldTeleport)
            {
                // 터치패드에서 손을 떼면, 모든 시각적 표시를 제거. 실제로 텔레포트 함
                if (shouldTeleport && Pointer.PointOnNavMesh)
                {
                    // 텔레포트 시작.
                    CurrentTeleportState = TeleportState.Teleporting;
                    TeleportTimeMarker = Time.time;
                }
                // 사용자가 취소하면, 시각적 표시를 제거하고 아무것도 하지 않음.
                else
                {
                    CurrentTeleportState = TeleportState.None;
                }
                
                // 컨트롤러 재설정
                ActiveController = null;
                // 포인터 사용 안 함
                Pointer.enabled = false;
                // 시각적 표시 사용 안 함
                RoomBorder.enabled = false;
                // RoomBorder.Transpose = Matrix4x4.TRS(OriginTransform.position, Quaternion.identity, Vector3.one);
                if (NavmeshAnimator != null)
                {
                    NavmeshAnimator.SetBool(EnabledAnimatorID, false);
                }
                Pointer.transform.parent = null;
                Pointer.transform.position = Vector3.zero;
                Pointer.transform.rotation = Quaternion.identity;
                Pointer.transform.localScale = Vector3.one;
            } 
            else
            {
                // 플레이어는 여전히 텔레포트 위치 결정 중. 즉, 터치패드 누르는 중.
                // Note: 포물선 Pointer / Marker 렌더링이 포물선 포인터에서 수행.
                Vector3 offset = HeadTransform.position - OriginTransform.position;
                offset.y = 0;

                // 텔레포트 후, 보호자 경계선을 표시할 위치 표시
                RoomBorder.enabled = Pointer.PointOnNavMesh;
                RoomBorder.Transpose = Matrix4x4.TRS(Pointer.SelectedPoint - offset, Quaternion.identity, Vector3.one);

                // 햅틱 진동 Part. 최대 각도 일때는 클릭 불가
                if (Pointer.CurrentParabolaAngleY >= 45)
                {
                    LastClickAngle = Pointer.CurrentPointVector;
                }

                float angleClickDiff = Vector3.Angle(LastClickAngle, Pointer.CurrentPointVector);
                if (IsClicking && Mathf.Abs(angleClickDiff) > HapticClickAngleStep)
                {
                    LastClickAngle = Pointer.CurrentPointVector;
                    //// 진동 불편하면 이 부분 지우기.
                    //if (Pointer.PointOnNavMesh)
                    //{
                    //    // 어떠한 컨트롤러를 눌러도 진동이 나게 함.
                    //    haptic.Execute(0, 0.1f, 150f, 75f, SteamVR_Input_Sources.Any);
                    //}
                }

                // 텔레포트가능한 지면에 진입시 더 강한 진동 트리거.
                if (Pointer.PointOnNavMesh && !IsClicking)
                {
                    IsClicking = true;
                    haptic.Execute(0, 0.05f, 75f, 75f, SteamVR_Input_Sources.Any);
                    LastClickAngle = Pointer.CurrentPointVector;
                }
                else if (!Pointer.PointOnNavMesh && IsClicking)
                    IsClicking = false;
            }
        }
        // Teleport 초기 진입할 때 시작하는 부분. 현재 텔레포트 상태 = 없음
        else
        { 
            // 이때 플레이어는 터치패드를 누르지 않거나 취소한 적이 없음.
            // 터치패드에서 손을 뗌. 플레이어가 터치패드를 누르고 시각적 표시기를 활성화할 때까지 대기.
            foreach (SteamVR_Behaviour_Pose obj in Controllers)
            {
                if (teleport.GetStateDown(SteamVR_Input_Sources.Any))
                {
                    // 컨트롤러를 이 것으로 설정. 포물선 포인터와 시각적 표시기를 활성화. 사용자가 텔레포트 할 수 있는 위치를 결정하는 데 사용.
                    ActiveController = obj;

                    Pointer.transform.parent = obj.transform;
                    Pointer.transform.localPosition = Vector3.zero;
                    Pointer.transform.localRotation = Quaternion.identity;
                    Pointer.transform.localScale = Vector3.one;
                    Pointer.enabled = true;

                    // Selecting으로 변경해줌.
                    CurrentTeleportState = TeleportState.Selecting;
                    
                    if(NavmeshAnimator != null)
                        NavmeshAnimator.SetBool(EnabledAnimatorID, true);

                    Pointer.ForceUpdateCurrentAngle();
                    LastClickAngle = Pointer.CurrentPointVector;
                    IsClicking = Pointer.PointOnNavMesh;
                }
            }
        }
	}
}

// 플레이어가 현재 텔레포트를 사용하고 있음을 나타냄.
public enum TeleportState
{
    // 플레이어가 텔레포트를 사용 안함.
    None,
    // 플레이어가 현재 텔레포트 대상을 선택
    Selecting,
    // 플레이어가 텔레포트 대상을 선택. 이동
    Teleporting
}