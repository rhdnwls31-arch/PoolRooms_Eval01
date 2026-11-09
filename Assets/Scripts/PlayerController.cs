using UnityEngine;
using UnityEngine.InputSystem; // 이 프로젝트는 새 Input System을 쓰므로 필요

// 1인칭 이동과 시점 회전을 담당하는 스크립트
public class PlayerController : MonoBehaviour
{
    public Transform cameraTransform;   // 상하 시점을 돌릴 카메라 (Inspector에서 연결)
    public float walkSpeed = 3f;        // 걷기 속도
    public float runSpeed = 5.5f;       // Shift를 눌렀을 때 속도
    public float mouseSensitivity = 0.1f; // 마우스 감도

    CharacterController controller;     // 충돌과 이동을 처리하는 컴포넌트
    float cameraPitch = 0f;             // 카메라의 위아래 각도
    float verticalVelocity = 0f;        // 중력용 속도

    void Start()
    {
        controller = GetComponent<CharacterController>();
        // 마우스 커서를 화면 가운데 고정하고 숨김
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    void Update()
    {
        Look();
        Move();
    }

    // 마우스로 시점 돌리기
    void Look()
    {
        Vector2 mouseDelta = Mouse.current.delta.ReadValue() * mouseSensitivity;

        // 좌우: 플레이어 몸 전체를 회전
        transform.Rotate(0f, mouseDelta.x, 0f);

        // 상하: 카메라만 회전 (고개를 너무 숙이거나 젖히지 않게 제한)
        cameraPitch -= mouseDelta.y;
        cameraPitch = Mathf.Clamp(cameraPitch, -80f, 80f);
        cameraTransform.localRotation = Quaternion.Euler(cameraPitch, 0f, 0f);
    }

    // WASD로 이동하기
    void Move()
    {
        var kb = Keyboard.current;
        float x = 0f, z = 0f;
        if (kb.wKey.isPressed) z += 1f;
        if (kb.sKey.isPressed) z -= 1f;
        if (kb.dKey.isPressed) x += 1f;
        if (kb.aKey.isPressed) x -= 1f;

        float speed = kb.leftShiftKey.isPressed ? runSpeed : walkSpeed;
        Vector3 input = transform.right * x + transform.forward * z;
        Vector3 move = input.sqrMagnitude > 0f ? input.normalized * speed : Vector3.zero;

        // 중력: 바닥에 붙어 있으면 약하게 눌러 주고, 공중이면 점점 빨라지게
        if (controller.isGrounded) verticalVelocity = -1f;
        else verticalVelocity += Physics.gravity.y * Time.deltaTime;
        move.y = verticalVelocity;

        controller.Move(move * Time.deltaTime);
    }
}