using PlayniteWebEmulator.Emulation;
using System;
using System.Linq;
using System.Net;

namespace PlayniteWebEmulator.Hosting
{
    internal static class JsDosPlayerPage
    {
        public static string Build(string gameName, JsDosLaunchPlan plan)
        {
            if (string.IsNullOrWhiteSpace(gameName)) throw new ArgumentException("A game name is required.", nameof(gameName));
            if (plan == null) throw new ArgumentNullException(nameof(plan));
            var files = string.Join(",", plan.Files.Select(file =>
                "{path:'" + JavaScript(file.RelativePath) + "',url:'./game/" + EscapePath(file.RelativePath) + "',size:" + file.Size + "}"));
            var launchDirectory = string.Join("\\", plan.LaunchRelativePath.Split('/').Reverse().Skip(1).Reverse());
            var launchFile = plan.LaunchRelativePath.Split('/').Last();
            var autoexec = "@echo off\nmount c .\nc:\n" +
                (string.IsNullOrWhiteSpace(launchDirectory) ? string.Empty : "cd " + launchDirectory + "\n") +
                launchFile + "\n";

            return "<!doctype html><html lang=\"en\"><head><meta charset=\"utf-8\">" +
                "<meta name=\"viewport\" content=\"width=device-width,initial-scale=1\">" +
                $"<title>{WebUtility.HtmlEncode(gameName)} — Web Emulator</title>" +
                "<link rel=\"stylesheet\" href=\"./runtime/js-dos.css\"><style>" +
                "html,body,#player{width:100%;height:100%;margin:0;background:#000;overflow:hidden}" +
                "#player canvas{cursor:none!important}" +
                "#status{position:fixed;left:1rem;right:1rem;bottom:1rem;padding:.8rem 1rem;background:rgba(12,18,30,.94);border:1px solid #475569;border-radius:.6rem;color:#fff;font:14px Segoe UI,sans-serif;z-index:9999}" +
                "#status.error{background:rgba(127,29,29,.96);border-color:#ef4444}#progress{width:100%;height:.55rem;margin-top:.55rem}" +
                "</style></head><body><div id=\"player\"></div><div id=\"status\"><span id=\"statusText\">Loading DOS game files…</span><progress id=\"progress\" max=\"1\" value=\"0\"></progress></div>" +
                "<script src=\"./runtime/js-dos.js\"></script><script>" +
                "const files=[" + files + "],box=document.getElementById('status'),text=document.getElementById('statusText'),bar=document.getElementById('progress');" +
                "function report(event,detail){const b=new Image();b.src='./diagnostics?event='+encodeURIComponent(event)+'&detail='+encodeURIComponent(detail||'')+'&nonce='+Date.now();}" +
                "function status(value,error){text.textContent=value||'Ready.';box.className=error?'error':'';if(!value&&!error)box.style.display='none';}" +
                "async function binary(file,index){status('Loading DOS game data ('+(index+1)+'/'+files.length+'): '+file.path);const response=await fetch(file.url,{cache:'no-store'});if(!response.ok)throw new Error('Unable to load '+file.path+': '+response.status);const contents=new Uint8Array(await response.arrayBuffer());bar.value=index+1;return {path:file.path,contents:contents};}" +
                "async function start(){if(typeof window.Dos!=='function')throw new Error('The js-dos player did not load.');bar.max=Math.max(1,files.length);const initFs=[];for(let index=0;index<files.length;index++)initFs.push(await binary(files[index],index));status('Starting js-dos…');" +
                "const player=window.Dos(document.getElementById('player'),{autoStart:true,pathPrefix:'./runtime/emulators/',background:'#000000',noCloud:true,noNetworking:true,kiosk:false,workerThread:true,dosboxConf:'[autoexec]\\n" + JavaScript(autoexec) + "',initFs:initFs});status('');report('ready','js-dos launched " + JavaScript(plan.LaunchRelativePath) + "');return player;}" +
                "let closing=false;function close(){if(closing)return;closing=true;navigator.sendBeacon('./diagnostics?event=closed&detail=Browser+tab+closed','');}window.addEventListener('pagehide',close);window.addEventListener('beforeunload',close);setInterval(function(){report('heartbeat','');},5000);" +
                "window.addEventListener('error',function(event){status('js-dos failed: '+(event.message||'browser error'),true);report('error',event.message||'browser error');});" +
                "start().catch(function(error){status(error.message||String(error),true);report('error',error.message||String(error));});" +
                "</script></body></html>";
        }

        private static string EscapePath(string value) => string.Join("/", value.Split('/').Select(Uri.EscapeDataString));
        private static string JavaScript(string value) => (value ?? string.Empty).Replace("\\", "\\\\").Replace("'", "\\'").Replace("\r", "\\r").Replace("\n", "\\n");
    }
}
