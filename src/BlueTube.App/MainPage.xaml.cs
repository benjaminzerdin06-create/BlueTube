namespace BlueTube.App;

public partial class MainPage : ContentPage
{
#if ANDROID
    private Android.Webkit.WebView? _webView;
    private FullscreenChromeClient? _chrome;
#endif

    public MainPage()
    {
        InitializeComponent();
        blazorWebView.BlazorWebViewInitialized += (_, e) =>
        {
#if ANDROID
            _webView = e.WebView;
            // Shorts start playing on swipe. Needs autoplay without a tap.
            e.WebView.Settings.MediaPlaybackRequiresUserGesture = false;
            _chrome = new FullscreenChromeClient(Platform.CurrentActivity!);
            e.WebView.SetWebChromeClient(_chrome);
#endif
        };
    }

    /// <summary>Back: leave fullscreen, then go back in app history, then exit.</summary>
    protected override bool OnBackButtonPressed()
    {
#if ANDROID
        if (_chrome?.IsFullscreen == true)
        {
            _chrome.OnHideCustomView();
            return true;
        }
        if (_webView?.CanGoBack() == true)
        {
            _webView.GoBack();
            return true;
        }
#endif
        return base.OnBackButtonPressed();
    }
}
