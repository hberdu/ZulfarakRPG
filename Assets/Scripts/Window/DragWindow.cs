using UnityEngine;
using UnityEngine.EventSystems;

namespace ZulfarakRPG
{
    // Attach to any UI element that should act as the drag handle.
    public class DragWindow : MonoBehaviour, IPointerDownHandler, IDragHandler
    {
        private int _dragStartWinX;
        private int _dragStartWinY;
        private int _dragStartCursorX;
        private int _dragStartCursorY;

        public void OnPointerDown(PointerEventData data)
        {
#if !UNITY_EDITOR
            if (OverlayWindow.GetOsCursorPos(out var p))
            {
                _dragStartCursorX = p.X;
                _dragStartCursorY = p.Y;
                _dragStartWinX    = OverlayWindow.WinX;
                _dragStartWinY    = OverlayWindow.WinY;
            }
#endif
        }

        public void OnDrag(PointerEventData data)
        {
#if !UNITY_EDITOR
            if (!OverlayWindow.GetOsCursorPos(out var p)) return;
            int dx = p.X - _dragStartCursorX;
            int dy = p.Y - _dragStartCursorY;
            OverlayWindow.MoveWindowTo(_dragStartWinX + dx, _dragStartWinY + dy);
#endif
        }
    }
}
