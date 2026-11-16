using Android.App;
using Android.Content.PM;
using Android.Views;
using Android.Webkit;
using Android.Widget;
using AndroidX.Core.View;
using View = Android.Views.View;

namespace BlueTube.App;

/// <summary>
/// Android WebView has no fullscreen video by default.
/// This client shows the player's fullscreen view over the app, in landscape, without system bars.
/// </summary>
public sealed class FullscreenChromeClient(Activity activity) : WebChromeClient
{
    private View? _fullscreenView;
    private ICustomViewCallback? _callback;
    private ScreenOrientation _oldOrientation;

    public bool IsFullscreen => _fullscreenView is not null;

    public override void OnShowCustomView(View? view, ICustomViewCallback? callback)
    {
        if (_fullscreenView is not null)
        {
            callback?.OnCustomViewHidden();
            return;
        }

        _fullscreenView = view;
        _callback = callback;
        _oldOrientation = activity.RequestedOrientation;

        var decor = (FrameLayout)activity.Window!.DecorView;
        decor.AddView(view, new FrameLayout.LayoutParams(ViewGroup.LayoutParams.MatchParent, ViewGroup.LayoutParams.MatchParent));

        var controller = WindowCompat.GetInsetsController(activity.Window, decor);
        if (controller is not null)
        {
            controller.Hide(WindowInsetsCompat.Type.SystemBars());
            controller.SystemBarsBehavior = WindowInsetsControllerCompat.BehaviorShowTransientBarsBySwipe;
        }
        activity.RequestedOrientation = ScreenOrientation.SensorLandscape;
    }

    public override void OnHideCustomView()
    {
        if (_fullscreenView is null) return;

        var decor = (FrameLayout)activity.Window!.DecorView;
        decor.RemoveView(_fullscreenView);
        WindowCompat.GetInsetsController(activity.Window, decor)?.Show(WindowInsetsCompat.Type.SystemBars());
        activity.RequestedOrientation = _oldOrientation;

        _fullscreenView = null;
        _callback?.OnCustomViewHidden();
        _callback = null;
    }
}
