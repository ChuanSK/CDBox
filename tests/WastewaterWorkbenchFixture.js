// Preview-only CAD transport. No settings or drawing data leave this document.
window.previewMessages=[];window.previewErrors=[];
window.addEventListener('error',e=>{previewErrors.push(e.message);document.documentElement.dataset.previewError=previewErrors.join('; ');});
const attrs={Enabled:true,ObjectKind:'主管',Material:'HDPE',Diameter:'DN300',StartNode:'W001',EndNode:'W002',StartDepth:1.5,EndDepth:1.8,AverageDepth:1.65,TrenchWidth:.8,PipeOuterDiameter:.3,IsSpecialObject:false,ManualLength:60,UseManualLength:false};
const layers=[{Name:'原土回填',Height:1.2,Locked:false},{Name:'砂垫层',Height:.2,Locked:true,IsCushionLayer:true}];
const attributeContext={selected:true,documentId:'preview',documentName:'污水设计示例.dwg',handle:'A1',layerName:'污水主管',inferredKind:'主管',cadLength:60,attributes:attrs,layers,warnings:[],requestId:0};
const summary={totalPipeLength:284.5,mainPipeLength:220.3,branchPipeLength:64.2,excavationVolume:366.8,backfillVolume:218.4,beddingVolume:48.6,restorationArea:210.5,facilityCount:18,wellCount:16,otherFacilityCount:2,roadBreakingArea:220.8,earthworkOutVolume:89.6};
const snapshot={document:{id:'preview',name:'污水设计示例.dwg'},scope:{regionName:'整张图纸'},status:{message:'统计完成',updatedAt:'2026-09-09 10:00',dataCompleteness:96,issueCount:1},summary,sourceSummary:{propertyOrGeometryPercent:96,defaultEstimatePercent:4,failedPercent:0},qualityIssues:[],details:[],objectDetails:[]};
Object.assign(snapshot,{
 wells:{title:'井类',count:16,cumulativeDepth:32,averageDepth:2,excavationVolume:40,backfillVolume:18,beddingVolume:4,coverCount:16,byType:{检查井:16},bySpecification:{'1000':16}},
 mainPipes:{title:'主管',count:12,length:220.3,averageDepth:1.65,excavationVolume:250,backfillVolume:160,beddingVolume:34,restorationArea:160,byMaterial:{HDPE:220.3},bySpecification:{DN300:220.3},byLayerMaterial:{砂垫层:34}},
 branchPipes:{title:'支管',count:8,length:64.2,averageDepth:1.2,excavationVolume:66.8,backfillVolume:40.4,beddingVolume:10.6,restorationArea:50.5,byMaterial:{UPVC:64.2},bySpecification:{DN160:64.2}},
 others:{title:'其他设施',count:2,excavationVolume:10,byType:{化粪池:2}},
 referenceItems:[{item:'土方开挖',quantity:366.8,unit:'m³',remark:'沟槽与构筑物',source:'属性/几何'},{item:'砂垫层',quantity:48.6,unit:'m³',remark:'结构层计算',source:'属性/几何'}],
 details:[{handle:'A1',category:'主管',name:'W001—W002',layerName:'污水主管',material:'HDPE',specification:'DN300',length:60,depth:1.65,excavationVolume:79.2,backfillVolume:57.6,beddingVolume:9.6,restorationArea:48,source:'属性/几何',status:'已计算',issueCodes:[]}],
 qualityIssues:[{code:'missing-depth',severity:'warning',title:'部分检查井缺少井深',count:1,category:'井类',handles:['B1']}]
});
const options={Width:1.2,TextHeight:.08,LeftLabelWidth:.45,SectionTitle:'W001—W002',DrawTitle:true,DrawTopDimension:true,DrawRightDimensions:true,DrawTotalHeightDimension:true,Layers:[{Height:1.2,LeftLabel:'原土回填',DrawLayer:true,Pipes:[{Diameter:.3,PipeText:'DN300',VerticalMode:1}]},{Height:.2,LeftLabel:'砂垫层',DrawLayer:true,Pipes:[]}]};
window.previewAttributeContext=attributeContext;
window.chrome={webview:{postMessage(raw){previewMessages.push(raw);const [,route,data]=raw.split('|');let arg;try{arg=JSON.parse(decodeURIComponent(data||''))}catch{arg={}}
setTimeout(()=>{
 if(route==='getQuantityAttributeContext'||route==='selectQuantityAttributeObject')window.CDBoxQuantityAttributeEditorLoad(attributeContext);
 if(route==='calculateQuantityDraft')window.CDBoxQuantityAttributeEditorLoad({...attributeContext,...arg,attributes:arg.attributes,layers:arg.layers});
 if(route==='getQuantityContext')window.CDBoxQuantityDashboardContext({documents:[{id:'preview',name:'污水设计示例.dwg',isActive:true}],regions:[],request:{documentId:'preview',scopeType:'whole',liveMode:true},cachedSnapshot:snapshot});
 if(route==='getQuantitySnapshot'||route==='refreshQuantitySnapshot')window.CDBoxQuantityDashboardReceive(snapshot);
 if(route==='getSectionDrawingOptions')window.CDBoxSectionDrawingLoad({options,textStyles:['标准','宋体'],dimensionStyles:['标准'],layerNames:['0','污水断面'],hatchPatterns:['无填充','ANSI31']});
},25);
}}};
