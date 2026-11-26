"""Camera framing with the real Fantasy UI width and the device Safe Area (U1-ENV.3).

Python mirror of SanctuaryCameraFit.ComputeFraming, used by the tests. All insets are
in SCREEN HEIGHTS (pixels / Screen.height), measured from the screen edge:

  left   = max(design left margin, safe-area left inset)
  right  = reported Fantasy UI occlusion (distance from the right edge to the panel's
           left edge), or design panel + safe-area right inset when nothing is reported;
           never less than the safe-area right inset
  top    = max(design top margin, safe-area top inset)
  bottom = max(design bottom margin, safe-area bottom inset)

The orthographic size only grows (never zooms in): enough to keep the design free
width at 16:9 and the design free height. The camera then shifts so the free area
stays centred where the design centred it. With the design insets this gives
exactly the U1-ENV.2 pose.
"""
import layout

D_LEFT = layout.LEFT_MARGIN_HEIGHTS
D_RIGHT = layout.RIGHT_PANEL_HEIGHTS
D_TOP = layout.TOP_MARGIN_HEIGHTS
D_BOTTOM = layout.BOTTOM_MARGIN_HEIGHTS
MIN_ASPECT = 16 / 9


def insets(ui_right=None, safe_left=0.0, safe_right=0.0, safe_top=0.0, safe_bottom=0.0):
    right = (D_RIGHT + safe_right) if ui_right is None else max(ui_right, safe_right)
    return (max(D_LEFT, safe_left), right, max(D_TOP, safe_top), max(D_BOTTOM, safe_bottom))


def compute(aspect, ins, base=layout.ORTHO_SIZE):
    """Returns (ortho_size, shift_right, shift_up) relative to the camera anchor."""
    left, right, top, bottom = ins
    need_w = (MIN_ASPECT - D_LEFT - D_RIGHT) / max(aspect - left - right, 0.05)
    need_h = (1 - D_TOP - D_BOTTOM) / max(1 - top - bottom, 0.05)
    size = base * max(1.0, need_w, need_h)
    return size, size * (right - left), size * ((bottom - D_BOTTOM) - (top - D_TOP))


def to_screen(p, aspect, ins):
    size, sr, su = compute(aspect, ins)
    right, up, _ = layout._cam_basis()
    a = layout.camera_anchor()
    c = [a[i] + right[i] * sr + up[i] * su for i in range(3)]
    d = [p[i] - c[i] for i in range(3)]
    sx = sum(d[i] * right[i] for i in range(3))
    sy = sum(d[i] * up[i] for i in range(3))
    return 0.5 + sx / (2 * size * aspect), 0.5 + sy / (2 * size)


def free_rect(aspect, ins):
    left, right, top, bottom = ins
    return left / aspect, bottom, 1 - right / aspect, 1 - top


# Device-like cases (insets in screen heights) the tests check every point against.
SCENARIOS = {
    "design": {},
    "notch_left": {"safe_left": 0.12},
    "notch_both_home_bar": {"safe_left": 0.15, "safe_right": 0.15, "safe_bottom": 0.055},
    "wide_ui_panel": {"ui_right": 0.62},
    "ui_reported_narrow": {"ui_right": 0.42},
    "tablet_like_safe_top": {"safe_top": 0.08, "safe_bottom": 0.08},
}
