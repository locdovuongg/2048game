using UnityEngine;

public class SwipeInput : MonoBehaviour
{
    [Header("Settings")]
    [SerializeField] private float minSwipeDistance = 50f;  // pixel tối thiểu
    [SerializeField] private float maxSwipeTime     = 0.5f; // giây tối đa

    private Vector2 startTouchPos;
    private float   startTime;
    private bool    isSwiping = false;

    private BoardManager boardManager;

    private void Start()
    {
        boardManager = FindFirstObjectByType<BoardManager>();
    }

    private void Update()
    {
        HandleTouchInput();
        HandleMouseInput();
    }

    private void HandleTouchInput()
    {
        if (Input.touchCount == 0) return;

        Touch touch = Input.GetTouch(0);

        if (touch.phase == TouchPhase.Began)
        {
            startTouchPos = touch.position;
            startTime     = Time.time;
            isSwiping     = true;
        }
        else if (touch.phase == TouchPhase.Ended && isSwiping)
        {
            isSwiping = false;
            float duration = Time.time - startTime;
            if (duration > maxSwipeTime) return;

            Vector2 delta = touch.position - startTouchPos;
            ProcessSwipe(delta);
            if (delta.magnitude < minSwipeDistance)
                NightModeHazard.Instance?.RegisterTap();
        }
        else if (touch.phase == TouchPhase.Canceled) 
        {
            isSwiping = false;
        }
    }

    private Vector2 mouseStartPos;
    private float   mouseStartTime;
    private bool    mouseSwipe = false;

    private void HandleMouseInput()
    {
        if (Input.GetMouseButtonDown(0))
        {
            mouseStartPos  = Input.mousePosition;
            mouseStartTime = Time.time;
            mouseSwipe     = true;
        }
        else if (Input.GetMouseButtonUp(0) && mouseSwipe)
        {
            mouseSwipe = false;
            float duration = Time.time - mouseStartTime;
            if (duration > maxSwipeTime) return;

            Vector2 delta = (Vector2)Input.mousePosition - mouseStartPos;
            if (delta.magnitude < minSwipeDistance)
            {
                NightModeHazard.Instance?.RegisterTap();
                return;
            }
            ProcessSwipe(delta);
        }
    }

    private void ProcessSwipe(Vector2 delta)
    {
        if (delta.magnitude < minSwipeDistance) return;
        if (boardManager == null) return;

        if (Mathf.Abs(delta.x) > Mathf.Abs(delta.y))
        {
            // Ngang
            if (delta.x > 0)
                boardManager.Move(Vector2Int.right);
            else
                boardManager.Move(Vector2Int.left);
        }
        else
        {
            // Dọc
            if (delta.y > 0)
                boardManager.Move(Vector2Int.up);
            else
                boardManager.Move(Vector2Int.down);
        }
    }
}