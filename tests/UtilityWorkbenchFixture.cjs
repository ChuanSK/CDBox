exports.layers = {
 presets:[{id:'default',name:'常用管线图层',isBuiltIn:true,description:'预览测试',layers:['污水主管-DN300-PVC','检查井-D700']}],
 success:true,parentOptions:['主管','井','注记','房屋'],categoriesByParent:{主管:['雨水','污水'],井:['检查井','沉泥井'],注记:['边长','高程'],房屋:['建筑物']},tagOptions:['PVC','混凝土','待复核'],
 layers:Array.from({length:320},(_,i)=>({name:['污水主管-DN300-PVC','检查井-沉泥井-D700','房屋边长注记','建筑物外轮廓'][i%4]+'-'+String(i+1).padStart(3,'0'),parent:['主管','井','注记','房屋'][i%4],category:['污水','沉泥井','边长','建筑物'][i%4],tags:i%4===0?['PVC','待复核']:[],recognitionStatus:['标准','兼容','信息不完整','未识别','冲突'][i%5],confidence:.85,colorHex:['#FF5555','#55BB77','#6699FF','#AAAAAA'][i%4],colorName:['红色','绿色','蓝色','随层'][i%4],colorIndex:7,colorType:'ACI',colorRgb:'255, 85, 85',linetype:'Continuous',isPlottable:true,isLocked:false,isFrozen:false,isOff:false,isCurrent:i===0,recognizedParent:'主管',recognizedCategory:'污水'}))
};
