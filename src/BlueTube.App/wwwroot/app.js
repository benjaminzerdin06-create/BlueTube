// Small JS helpers. Blazor calls them through IJSRuntime.
window.blueTube = {
    // Pull down at the top of the page to refresh. Calls .NET method "OnPullRefresh".
    pullToRefresh(el, dotnet) {
        const threshold = 70;
        let startY = null;
        let armed = false;

        el.addEventListener('touchstart', e => {
            startY = window.scrollY <= 0 ? e.touches[0].clientY : null;
        }, { passive: true });

        el.addEventListener('touchmove', e => {
            if (startY === null) return;
            const dy = Math.max(0, e.touches[0].clientY - startY);
            el.style.setProperty('--pull', Math.min(dy, 100) + 'px');
            armed = dy > threshold;
        }, { passive: true });

        el.addEventListener('touchend', () => {
            el.style.setProperty('--pull', '0px');
            if (armed) dotnet.invokeMethodAsync('OnPullRefresh');
            startY = null;
            armed = false;
        });
    },

    // Shorts: report the item that fills most of the container. Calls .NET method "OnActive(index)".
    observeShorts(container, dotnet) {
        const io = new IntersectionObserver(entries => {
            for (const e of entries) {
                if (e.isIntersecting) dotnet.invokeMethodAsync('OnActive', Number(e.target.dataset.index));
            }
        }, { root: container, threshold: 0.6 });
        container.querySelectorAll('[data-index]').forEach(el => io.observe(el));
    },

    // YouTube IFrame API command over postMessage (needs enablejsapi=1). Example: "pauseVideo".
    playerCommand(iframeId, func) {
        const frame = document.getElementById(iframeId);
        frame?.contentWindow?.postMessage(JSON.stringify({ event: 'command', func, args: [] }), 'https://www.youtube.com');
    },
};
