(function(){
 const host=location.hostname;
 if(!(host==='bilibili.com'||host.endsWith('.bilibili.com')))return;
 if(!document.getElementById('mini-browser-style')){
  const style=document.createElement('style');style.id='mini-browser-style';style.textContent=`
   ::-webkit-scrollbar{width:7px;height:7px}::-webkit-scrollbar-track{background:transparent}::-webkit-scrollbar-thumb{background:#70758088;border-radius:8px}
   html.mini-cinema,html.mini-cinema body{overflow:hidden!important;background:#000!important}
   html.mini-cinema #bilibili-player,html.mini-cinema #bilibiliPlayer,html.mini-cinema .bpx-player-container{position:fixed!important;inset:0!important;width:100vw!important;height:100vh!important;max-width:none!important;max-height:none!important;z-index:2147483000!important;margin:0!important}
   html.mini-cinema .bpx-player-video-area{width:100%!important;height:100%!important}
   html.mini-cinema .bpx-player-video-wrap{width:100%!important;height:100%!important}
  `;document.head.appendChild(style);
 }
 return !!document.querySelector('#bilibili-player,#bilibiliPlayer,.bpx-player-container');
})()
