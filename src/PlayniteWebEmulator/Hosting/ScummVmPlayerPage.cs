using PlayniteWebEmulator.Emulation;
using System;
using System.Linq;
using System.Net;

namespace PlayniteWebEmulator.Hosting
{
    internal static class ScummVmPlayerPage
    {
        public static string Build(string gameName, ScummVmLaunchPlan plan)
        {
            if (string.IsNullOrWhiteSpace(gameName)) throw new ArgumentException("A game name is required.", nameof(gameName));
            if (plan == null) throw new ArgumentNullException(nameof(plan));

            var files = string.Join(",", plan.Files.Select(file =>
                "{path:'" + JavaScript(file.RelativePath) + "',url:'./game/" + EscapePath(file.RelativePath) + "',size:" + file.Size + "}"));
            var enginePluginUrl = "./runtime/data/plugins/" + Uri.EscapeDataString(plan.EnginePluginFileName);

            return "<!doctype html><html lang=\"en\"><head><meta charset=\"utf-8\">" +
                "<meta name=\"viewport\" content=\"width=device-width,initial-scale=1\">" +
                $"<title>{WebUtility.HtmlEncode(gameName)} — Web Emulator</title><style>" +
                "html,body{width:100%;height:100%;margin:0;background:#000;overflow:hidden;color:#fff;font-family:Segoe UI,sans-serif}" +
                "#canvas{display:block;width:100%;height:100%;outline:none;background:#000}" +
                "#status{position:fixed;left:1rem;right:1rem;bottom:1rem;padding:.8rem 1rem;background:rgba(12,18,30,.92);border:1px solid #475569;border-radius:.6rem;z-index:3}" +
                "#status.error{background:rgba(127,29,29,.96);border-color:#ef4444}" +
                "#progress{width:100%;height:.55rem;margin-top:.55rem}" +
                "#download-modal{display:none;position:fixed;left:50%;top:50%;width:min(34rem,calc(100% - 3rem));transform:translate(-50%,-50%);padding:1rem 1.2rem;background:rgba(12,18,30,.96);border:1px solid #475569;border-radius:.6rem;z-index:4}" +
                "#download-modal-title{font-weight:600;margin-bottom:.7rem}" +
                "#download-modal-progress{height:.55rem;background:#1e293b;border-radius:999px;overflow:hidden}" +
                "#download-modal-progress-fill{height:100%;width:0;background:#38bdf8}" +
                "#download-modal-progress-text,#download-modal-speed-text{margin-top:.45rem;color:#cbd5e1;font-size:.85rem}" +
                "</style></head><body><canvas id=\"canvas\" tabindex=\"-1\" oncontextmenu=\"event.preventDefault()\"></canvas>" +
                "<div id=\"status\"><span id=\"statusText\">Preparing ScummVM…</span><progress id=\"progress\" max=\"1\" value=\"0\"></progress></div>" +
                "<div id=\"download-modal\"><div id=\"download-modal-title\"><span>Loading ScummVM…</span></div>" +
                "<div id=\"download-modal-progress\"><div id=\"download-modal-progress-fill\"></div></div>" +
                "<div id=\"download-modal-progress-text\"></div><div id=\"download-modal-speed-text\"></div></div>" +
                "<script>" +
                "const files=[" + files + "];const canvas=document.getElementById('canvas'),box=document.getElementById('status'),text=document.getElementById('statusText'),bar=document.getElementById('progress');" +
                "function report(event,detail){const b=new Image();b.src='./diagnostics?event='+encodeURIComponent(event)+'&detail='+encodeURIComponent(detail||'')+'&nonce='+Date.now();}" +
                "function status(value,error){text.textContent=value||'Ready.';box.className=error?'error':'';if(!value&&!error)box.style.display='none';}" +
                "function ensureDir(path){let current='';for(const part of path.split('/').filter(Boolean)){current+='/'+part;if(!FS.analyzePath(current).exists)FS.mkdir(current);}}" +
                "async function binary(url){const response=await fetch(url,{cache:'no-store'});if(!response.ok)throw new Error('Unable to load '+url+': '+response.status);return new Uint8Array(await response.arrayBuffer());}" +
                "const support=[" +
                "{url:'./runtime/data/translations.dat',path:'/data/translations.dat'}," +
                "{url:'./runtime/data/gui-icons.dat',path:'/data/gui-icons.dat'}," +
                "{url:'./runtime/data/scummremastered.zip',path:'/data/scummremastered.zip'}," +
                "{url:'" + JavaScript(enginePluginUrl) + "',path:'/plugins/" + JavaScript(plan.EnginePluginFileName) + "'}];" +
                "let payloads=[];async function prepare(){let done=0,total=support.length+files.length;bar.max=total;" +
                "for(const asset of support){status('Loading ScummVM runtime: '+asset.path);payloads.push({path:asset.path,data:await binary(asset.url)});bar.value=++done;}" +
                "for(const file of files){status('Loading game data ('+(done-support.length+1)+'/'+files.length+'): '+file.path);payloads.push({path:'/games/game/'+file.path,data:await binary(file.url)});bar.value=++done;}" +
                "}" +
                "function mount(){ensureDir('/data');ensureDir('/plugins');ensureDir('/games/game');ensureDir('/local/saves');for(const item of payloads){const parent=item.path.slice(0,item.path.lastIndexOf('/'));ensureDir(parent);FS.writeFile(item.path,item.data,{canOwn:true});item.data=null;}payloads=[];FS.writeFile('/local/scummvm.ini','[scummvm]\\npluginspath=/plugins\\nextrapath=/data\\niconspath=/data\\nthemepath=/data\\nsavepath=/local/saves\\n');report('mounted',files.length+' game files');}" +
                "window.location.hash=encodeURI('--path=/games/game --auto-detect --extrapath=/data --iconspath=/data --themepath=/data --savepath=/local/saves');" +
                "window.Module={canvas:canvas,preRun:[mount],locateFile:function(path){return './runtime/'+path;},print:function(value){console.log(value);},printErr:function(value){console.error(value);},setStatus:function(value){status(value||'');},monitorRunDependencies:function(left){if(left===0)status('Starting ScummVM…');},onRuntimeInitialized:function(){if(typeof httpHideProgressBar==='function')httpHideProgressBar();status('');report('ready','ScummVM runtime ready');},onAbort:function(value){status('ScummVM aborted: '+value,true);report('abort',String(value));}};" +
                "let closing=false;function close(){if(closing)return;closing=true;navigator.sendBeacon('./diagnostics?event=closed&detail=Browser+tab+closed','');}window.addEventListener('pagehide',close);window.addEventListener('beforeunload',close);setInterval(function(){report('heartbeat','');},5000);" +
                "window.addEventListener('error',function(event){status('ScummVM failed: '+(event.message||'browser error'),true);report('error',event.message||'browser error');});" +
                "prepare().then(function(){status('Starting ScummVM…');const script=document.createElement('script');script.src='./runtime/scummvm.js';script.async=true;script.onerror=function(){status('Unable to load ScummVM runtime.',true);};document.body.appendChild(script);}).catch(function(error){status(error.message||String(error),true);report('error',error.message||String(error));});" +
                "</script></body></html>";
        }

        private static string EscapePath(string value) =>
            string.Join("/", value.Split('/').Select(Uri.EscapeDataString));

        private static string JavaScript(string value) =>
            (value ?? string.Empty).Replace("\\", "\\\\").Replace("'", "\\'").Replace("\r", "\\r").Replace("\n", "\\n");
    }
}
