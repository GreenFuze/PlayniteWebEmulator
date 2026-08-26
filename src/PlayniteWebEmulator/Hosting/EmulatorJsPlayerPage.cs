using PlayniteWebEmulator.Emulation;
using System;
using System.Net;

namespace PlayniteWebEmulator.Hosting
{
    internal static class EmulatorJsPlayerPage
    {
        public static string Build(BrowserEmulatorProfile profile, string gameName)
        {
            if (profile == null) throw new ArgumentNullException(nameof(profile));
            if (!string.Equals(profile.RuntimeId, "emulatorjs", StringComparison.Ordinal) || string.IsNullOrWhiteSpace(profile.CoreId))
                throw new InvalidOperationException($"Profile '{profile.Id}' is not an EmulatorJS profile.");

            return "<!doctype html><html lang=\"en\"><head><meta charset=\"utf-8\">" +
                "<meta name=\"viewport\" content=\"width=device-width,initial-scale=1\">" +
                $"<title>{Encode(gameName)} — Web Emulator</title><style>" +
                "html,body,#game{width:100%;height:100%;margin:0;background:#000;overflow:hidden}" +
                "#failure{display:none;position:fixed;inset:0;place-items:center;padding:2rem;background:#090d18;color:#fff;font:18px Segoe UI,sans-serif;z-index:10}" +
                "</style></head><body><div id=\"game\"></div><div id=\"failure\"></div><script>" +
                "function report(event,detail){var beacon=new Image();beacon.src='./diagnostics?event='+encodeURIComponent(event)+'&detail='+encodeURIComponent(detail||'')+'&nonce='+Date.now();}" +
                "var nativeFetch=window.fetch.bind(window);window.fetch=function(input,init){var target=String(input);if(target==='https://cdn.emulatorjs.org/stable/data/version.json'){input='./runtime/version.json';target=String(input);}return nativeFetch(input,init).catch(function(error){report('fetch-error',target+': '+String(error&&error.message||error));throw error;});};" +
                "var sessionClosing=false;function closeSession(){if(sessionClosing)return;sessionClosing=true;navigator.sendBeacon('./diagnostics?event=closed&detail=Browser+tab+closed','');}" +
                "window.addEventListener('pagehide',closeSession);window.addEventListener('beforeunload',closeSession);setInterval(function(){report('heartbeat','');},5000);" +
                "window.addEventListener('error',function(event){var detail=event.message||'unknown browser error';var box=document.getElementById('failure');box.style.display='grid';box.textContent='EmulatorJS failed: '+detail;report('error',detail);});" +
                "window.addEventListener('unhandledrejection',function(event){var detail=String(event.reason&&event.reason.message||event.reason||'unknown rejected promise');report('rejection',detail);});" +
                "window.EJS_player='#game';" +
                $"window.EJS_core='{JavaScript(profile.CoreId)}';" +
                "window.EJS_gameUrl='./game';" +
                $"window.EJS_gameName='{JavaScript(gameName)}';" +
                $"window.EJS_controlScheme='{JavaScript(profile.ControlSchemeId)}';" +
                "window.EJS_pathtodata='./runtime/';" +
                "window.EJS_startOnLoaded=true;window.EJS_fullscreenOnLoaded=false;window.EJS_disableAutoLang=false;window.EJS_threads=true;window.EJS_DEBUG_XX=false;" +
                "window.EJS_ready=function(){report('ready','EmulatorJS player is ready');};" +
                "window.EJS_onGameStart=function(){report('started','Emulation started');};" +
                "report('page','Player page loaded');" +
                "</script><script src=\"./runtime/loader.js\"></script></body></html>";
        }

        private static string Encode(string value) => WebUtility.HtmlEncode(value ?? string.Empty);

        private static string JavaScript(string value) =>
            (value ?? string.Empty).Replace("\\", "\\\\").Replace("'", "\\'").Replace("\r", "\\r").Replace("\n", "\\n");
    }
}
