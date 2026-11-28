// Génère les PNG d icône depuis les SVG.
// Usage : node Tools/icon/export-icons.js Tools/icon Assets/CraneBay/Icons
// (nécessite Node et le paquet playwright avec Chromium)
const { chromium } = require('playwright'); const fs=require('fs');
const dir=process.argv[2], out=process.argv[3];
(async()=>{
 const b=await chromium.launch(); const p=await b.newPage({deviceScaleFactor:1});
 const src=f=>'data:image/svg+xml;base64,'+fs.readFileSync(f).toString('base64');
 const shot=async(name,size,html)=>{await p.setViewportSize({width:size,height:size});
   await p.setContent(`<html><body style="margin:0;background:transparent">${html}</body></html>`);
   await p.waitForTimeout(150); await p.screenshot({path:`${out}/${name}`,omitBackground:true});};
 const img=(f,s,off=0)=>`<img src="${src(f)}" style="position:absolute;left:${off}px;top:${off}px;width:${s}px;height:${s}px">`;
 // Couches adaptatives 432x432 (108 dp)
 await shot('icon_adaptive_background.png',432,img(`${dir}/background.svg`,432));
 await shot('icon_adaptive_foreground.png',432,img(`${dir}/foreground.svg`,432));
 // Icônes legacy et rondes 512x512 : on montre la zone centrale (72/108) comme sur un lanceur adaptatif
 const S=512, big=S*108/72, off=-(big-S)/2;
 const layers=img(`${dir}/background.svg`,big,off)+img(`${dir}/foreground.svg`,big,off);
 await shot('icon_legacy.png',S,`<div style="position:relative;width:${S}px;height:${S}px;border-radius:22%;overflow:hidden">${layers}</div>`);
 await shot('icon_round.png',S,`<div style="position:relative;width:${S}px;height:${S}px;border-radius:50%;overflow:hidden">${layers}</div>`);
 await b.close();})();
