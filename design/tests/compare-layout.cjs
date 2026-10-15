// 基线来自 1440/1120 × 900 的原生 Skia 截图边界量测；只计算这些区域的交并比。
const fs=require('node:fs'),path=require('node:path');
const directory=path.resolve(process.argv[2]||process.env.AVA_AUDIT_OUTPUT||path.join(__dirname,'../../artifacts/prototype-audit'));
const iou=(a,b)=>{const intersection=Math.max(0,Math.min(a.x+a.width,b.x+b.width)-Math.max(a.x,b.x))*Math.max(0,Math.min(a.y+a.height,b.y+b.height)-Math.max(a.y,b.y));return intersection/(a.width*a.height+b.width*b.height-intersection);};
let minimum=1,values=0;
for(const culture of ['zh-CN','en-US'])for(const width of [1440,1120]){
 const actual=JSON.parse(fs.readFileSync(path.join(directory,`workspace-${culture}-${width}.geometry.json`),'utf8'));
 const baseline={titlebar:[1,1,width-2,44],toolbar:[1,45,width-2,50],left:[1,95,360,592],diff:[386,123,width-405,544],commit:[1,424,360,263],log:[1,691,width-2,181],status:[1,872,width-2,27]};
 for(const [key,coordinates]of Object.entries(baseline)){
   const [x,y,w,h]=coordinates,score=iou(actual[key],{x,y,width:w,height:h});minimum=Math.min(minimum,score);values++;
   if(score<.95)throw Error(`Native baseline region differs: ${culture}/${width}/${key}, IoU=${score}`);
 }
}
console.log(`PASS ${values} measured layout regions, minimum IoU ${(minimum*100).toFixed(2)}%. This is regional geometry, not whole-page pixel similarity.`);
