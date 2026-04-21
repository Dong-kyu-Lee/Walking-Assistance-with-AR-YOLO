package com.unity.template.ar_mobile;

import android.app.Activity;
import android.view.ActionMode;
import android.view.KeyEvent;
import android.view.Menu;
import android.view.MenuItem;
import android.view.MotionEvent;
import android.view.SearchEvent;
import android.view.View;
import android.view.Window;
import android.view.WindowManager;
import android.view.accessibility.AccessibilityEvent;
import com.unity3d.player.UnityPlayer;

public class VolumeKeyPlugin {

    private static final long LONG_PRESS_MS = 500;
    private static long volumeUpPressTime = 0;
    private static long volumeDownPressTime = 0;

    private static class VolumeKeyCallback implements Window.Callback {
        private final Window.Callback original;

        VolumeKeyCallback(Window.Callback original) {
            this.original = original;
        }

        // ── 볼륨 키 처리 ──────────────────────────────────────
        @Override
        public boolean dispatchKeyEvent(KeyEvent event) {
            int code = event.getKeyCode();
            if (code == KeyEvent.KEYCODE_VOLUME_UP) {
                if (event.getAction() == KeyEvent.ACTION_DOWN)
                    volumeUpPressTime = System.currentTimeMillis();
                else if (event.getAction() == KeyEvent.ACTION_UP) {
                    long held = System.currentTimeMillis() - volumeUpPressTime;
                    UnityPlayer.UnitySendMessage("VolumeKeyReceiver",
                        held >= LONG_PRESS_MS ? "OnVolumeUpLong" : "OnVolumeUp", "");
                }
                return true;
            }
            if (code == KeyEvent.KEYCODE_VOLUME_DOWN) {
                if (event.getAction() == KeyEvent.ACTION_DOWN)
                    volumeDownPressTime = System.currentTimeMillis();
                else if (event.getAction() == KeyEvent.ACTION_UP) {
                    long held = System.currentTimeMillis() - volumeDownPressTime;
                    UnityPlayer.UnitySendMessage("VolumeKeyReceiver",
                        held >= LONG_PRESS_MS ? "OnVolumeDownLong" : "OnVolumeDown", "");
                }
                return true;
            }
            return original != null && original.dispatchKeyEvent(event);
        }

        // ── Window.Callback 나머지 메서드 전부 위임 ────────────
        @Override public boolean dispatchKeyShortcutEvent(KeyEvent e) { return original != null && original.dispatchKeyShortcutEvent(e); }
        @Override public boolean dispatchTouchEvent(MotionEvent e) { return original != null && original.dispatchTouchEvent(e); }
        @Override public boolean dispatchTrackballEvent(MotionEvent e) { return original != null && original.dispatchTrackballEvent(e); }
        @Override public boolean dispatchGenericMotionEvent(MotionEvent e) { return original != null && original.dispatchGenericMotionEvent(e); }
        @Override public boolean dispatchPopulateAccessibilityEvent(AccessibilityEvent e) { return original != null && original.dispatchPopulateAccessibilityEvent(e); }
        @Override public View onCreatePanelView(int f) { return original != null ? original.onCreatePanelView(f) : null; }
        @Override public boolean onCreatePanelMenu(int f, Menu m) { return original != null && original.onCreatePanelMenu(f, m); }
        @Override public boolean onPreparePanel(int f, View v, Menu m) { return original != null && original.onPreparePanel(f, v, m); }
        @Override public boolean onMenuItemSelected(int f, MenuItem i) { return original != null && original.onMenuItemSelected(f, i); }
        @Override public boolean onMenuOpened(int f, Menu m) { return original != null && original.onMenuOpened(f, m); }
        @Override public void onPanelClosed(int f, Menu m) { if (original != null) original.onPanelClosed(f, m); }
        @Override public void onWindowAttributesChanged(WindowManager.LayoutParams p) { if (original != null) original.onWindowAttributesChanged(p); }
        @Override public void onContentChanged() { if (original != null) original.onContentChanged(); }
        @Override public void onWindowFocusChanged(boolean h) { if (original != null) original.onWindowFocusChanged(h); }
        @Override public void onAttachedToWindow() { if (original != null) original.onAttachedToWindow(); }
        @Override public void onDetachedFromWindow() { if (original != null) original.onDetachedFromWindow(); }
        @Override public boolean onSearchRequested() { return original != null && original.onSearchRequested(); }
        @Override public boolean onSearchRequested(SearchEvent e) { return original != null && original.onSearchRequested(e); }
        @Override public ActionMode onWindowStartingActionMode(ActionMode.Callback c) { return original != null ? original.onWindowStartingActionMode(c) : null; }
        @Override public ActionMode onWindowStartingActionMode(ActionMode.Callback c, int t) { return original != null ? original.onWindowStartingActionMode(c, t) : null; }
        @Override public void onActionModeStarted(ActionMode m) { if (original != null) original.onActionModeStarted(m); }
        @Override public void onActionModeFinished(ActionMode m) { if (original != null) original.onActionModeFinished(m); }
    }

    public static void init() {
        final Activity activity = UnityPlayer.currentActivity;
        activity.runOnUiThread(() -> {
            final Window.Callback current = activity.getWindow().getCallback();
            if (current instanceof VolumeKeyCallback) return;
            activity.getWindow().setCallback(new VolumeKeyCallback(current));
        });
    }
}
