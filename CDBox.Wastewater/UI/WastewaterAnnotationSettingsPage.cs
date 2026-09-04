using System;
using System.Text;
using System.Web.Script.Serialization;
using CDBox.Shared.UI;
using TCPipeAutoDraw.Modules.NodeAnnotation;
using TCPipeAutoDraw.Modules.PipeLengthAnnotation;
using TCPipeAutoDraw.Modules.SurfaceAreaAnnotation;

namespace CDBox.Wastewater.UI
{
    /// <summary>
    /// 污水表面积、管线长度与节点注记的统一独立设置页。
    /// 页面、路由和设置持久化均由可选污水模块承载。
    /// </summary>
    public sealed class WastewaterAnnotationSettingsPage
    {
        public const string PageId = "wastewater-annotation-settings";
        private readonly ICDBoxPageAppearanceService _appearance;
        private readonly ICDBoxColorPickerService _colors;
        private readonly Action _runSurface;
        private readonly Action _runPipe;
        private readonly Action _runNode;
        private static readonly JavaScriptSerializer _serializer =
            new JavaScriptSerializer { MaxJsonLength = int.MaxValue };

        public WastewaterAnnotationSettingsPage(
            ICDBoxPageAppearanceService appearance,
            ICDBoxColorPickerService colors,
            Action runSurface, Action runPipe, Action runNode)
        {
            _appearance = appearance
                ?? throw new ArgumentNullException("appearance");
            _colors = colors ?? throw new ArgumentNullException("colors");
            _runSurface = runSurface
                ?? throw new ArgumentNullException("runSurface");
            _runPipe = runPipe ?? throw new ArgumentNullException("runPipe");
            _runNode = runNode ?? throw new ArgumentNullException("runNode");
        }

        public CDBoxPageDefinition CreatePage()
        {
            return new CDBoxPageDefinition(PageId, "污水标注设置",
                BuildDocument, Route)
            {
                Width = 1040,
                Height = 760,
                MinimumWidth = 820,
                MinimumHeight = 620
            };
        }

        private string BuildDocument()
        {
            CDBoxPageAppearance appearance = _appearance.GetAppearance()
                ?? new CDBoxPageAppearance();
            return BuildStandaloneDocument(appearance,
                SurfaceAreaAnnotationSettingsStore.Load(),
                PipeLengthAnnotationSettingsStore.Load(),
                NodeAnnotationSettingsStore.Load());
        }

        public static string BuildStandaloneDocument(
            CDBoxPageAppearance appearance,
            SurfaceAreaAnnotationOptions surface,
            PipeLengthAnnotationOptions pipe,
            NodeAnnotationOptions node)
        {
            appearance = appearance ?? new CDBoxPageAppearance();
            string initial = _serializer.Serialize(new SettingsEnvelope
            {
                surface = surface ?? SurfaceAreaAnnotationOptions.Default,
                pipe = pipe ?? PipeLengthAnnotationOptions.Default,
                node = node ?? NodeAnnotationOptions.Default
            });
            var html = new StringBuilder();
            html.Append("<!doctype html><html lang='zh-CN'><head><meta charset='utf-8'><meta name='viewport' content='width=device-width,initial-scale=1'><title>污水标注设置</title><style>")
                .Append(Styles()).Append("</style></head><body data-theme='")
                .Append(Escape(appearance.Theme)).Append("'><main><header><div><small>WASTEWATER ANNOTATION</small><h1>污水标注设置</h1></div><div><button id='defaults'>恢复默认</button><button id='save' class='primary'>保存设置</button></div></header><nav><button class='active' data-tab='surface'>表面积标注</button><button data-tab='pipe'>管线长度标注</button><button data-tab='node'>节点标注</button></nav>")
                .Append("<section id='surface'></section><section id='pipe' hidden></section><section id='node' hidden></section><div id='toast'></div></main><script>var state=")
                .Append(initial).Append(";").Append(Script())
                .Append("</script></body></html>");
            return html.ToString();
        }

        public static string BuildComponentScript()
        {
            return Script();
        }

        private CDBoxPageRouteResult Route(CDBoxPageRouteRequest request)
        {
            if (request == null) return new CDBoxPageRouteResult
                { Handled = false };
            string name = (request.Name ?? string.Empty).Trim()
                .ToLowerInvariant();
            if (name == "savewastewaterannotationsettings")
            {
                SettingsEnvelope envelope = _serializer
                    .Deserialize<SettingsEnvelope>(request.Argument)
                    ?? new SettingsEnvelope();
                SurfaceAreaAnnotationSettingsStore.SaveStrict(
                    envelope.surface ?? SurfaceAreaAnnotationOptions.Default);
                PipeLengthAnnotationSettingsStore.SaveStrict(
                    envelope.pipe ?? PipeLengthAnnotationOptions.Default);
                NodeAnnotationSettingsStore.SaveStrict(
                    envelope.node ?? NodeAnnotationOptions.Default);
                return new CDBoxPageRouteResult
                {
                    Handled = true,
                    ToastKind = "success",
                    ToastMessage = "污水标注设置已保存"
                };
            }
            if (name == "runwastewatersurfaceannotation")
                return Interaction(_runSurface, "正在选择表面积边界");
            if (name == "runwastewaterpipeannotation")
                return Interaction(_runPipe, "正在选择管线");
            if (name == "runwastewaternodeannotation")
                return Interaction(_runNode, "正在选择节点");
            if (name == "pickwastewaterannotationcolor")
                return PickColor(request.Argument);
            return new CDBoxPageRouteResult { Handled = false };
        }

        private CDBoxPageRouteResult PickColor(string argument)
        {
            ColorRequest request = _serializer.Deserialize<ColorRequest>(
                argument ?? string.Empty) ?? new ColorRequest();
            CDBoxModuleColor selected;
            if (!_colors.TryPick(CDBoxModuleColor.FromIndex(request.index),
                    out selected, false, false))
                return new CDBoxPageRouteResult { Handled = true };
            int index = selected == null ? request.index : selected.Index;
            if (index < 1 || index > 255) index = 7;
            return new CDBoxPageRouteResult
            {
                Handled = true,
                ExecuteScript = "window.applyPickedColor && window.applyPickedColor("
                    + _serializer.Serialize(new ColorRequest
                    { path = request.path, index = index }) + ");"
            };
        }

        private static CDBoxPageRouteResult Interaction(Action action,
            string message)
        {
            return new CDBoxPageRouteResult
            {
                Handled = true,
                ActionToRun = action,
                ToastKind = "info",
                ToastMessage = message
            };
        }

        private static string Escape(string value)
        {
            return (value ?? "light").Replace("&", "&amp;")
                .Replace("'", "&#39;").Replace("<", "&lt;")
                .Replace(">", "&gt;");
        }

        private static string Styles()
        {
            return @":root{--bg:#f4f7fb;--panel:#fff;--panel2:#f8fafc;--line:#dce5f1;--text:#172033;--muted:#64748b;--brand:#326fea}body[data-theme=dark]{--bg:#0f172a;--panel:#172033;--panel2:#111827;--line:#2b3a50;--text:#e5e7eb;--muted:#94a3b8}*{box-sizing:border-box}html,body{margin:0;min-height:100%;background:var(--bg);color:var(--text);font-family:'Microsoft YaHei UI','Segoe UI',sans-serif}main{max-width:1040px;margin:auto;padding:24px}header{display:flex;align-items:center;justify-content:space-between;gap:16px}h1{margin:4px 0 0;font-size:25px}small{color:var(--brand);font-weight:800;letter-spacing:1.4px}button{height:38px;border:1px solid var(--line);border-radius:10px;background:var(--panel);color:var(--text);padding:0 14px;font:inherit;font-weight:700;cursor:pointer}.primary{border:0;color:#fff;background:linear-gradient(135deg,#326fea,#7c3aed)}nav{display:grid;grid-template-columns:repeat(3,minmax(0,1fr));gap:12px;margin:22px 0 12px}nav button{height:82px;text-align:left;font-size:15px}nav button.active{border-color:var(--brand);color:var(--brand);background:#eaf2ff}section{display:grid;grid-template-columns:repeat(2,minmax(0,1fr));gap:12px;border:1px solid var(--line);border-radius:16px;background:var(--panel);padding:18px;box-shadow:0 10px 30px rgba(30,50,90,.05)}section[hidden]{display:none}.group{display:grid;grid-template-columns:repeat(2,minmax(0,1fr));gap:12px;align-content:start;border:1px solid var(--line);border-radius:13px;background:var(--panel2);padding:14px}.group h2{grid-column:1/-1;margin:0 0 3px;font-size:15px}.field{display:grid;gap:6px}.field.wide,.check.wide{grid-column:1/-1}.field span{font-size:12px;color:var(--muted);font-weight:700}.field input,.field select{width:100%;height:38px;border:1px solid var(--line);border-radius:9px;background:var(--panel);color:var(--text);padding:0 10px;font:inherit}.check{display:flex;align-items:center;gap:9px;min-height:38px}.check input{width:18px;height:18px}.run{grid-column:1/-1;justify-self:start;margin-top:5px}.color{display:flex;align-items:center;justify-content:space-between}.swatch{width:18px;height:18px;border-radius:5px;border:1px solid var(--line)}#toast{position:fixed;right:22px;bottom:20px;opacity:0;padding:10px 14px;border-radius:10px;background:#172033;color:#fff;transition:.18s}#toast.show{opacity:1}@media(max-width:760px){nav,section,.group{grid-template-columns:1fr}.field.wide,.check.wide,.run,.group h2{grid-column:auto}nav button{height:48px}}";
        }

        private static string Script()
        {
            return @"function post(n,a){try{chrome.webview.postMessage('studio|'+n+'|'+encodeURIComponent(a||''))}catch(e){}}function esc(v){return String(v==null?'':v).replace(/&/g,'&amp;').replace(/""/g,'&quot;').replace(/</g,'&lt;')}function field(label,key,type,wide){var v=active()[key],cls='field'+(wide?' wide':'');if(type==='check')return '<label class=""check '+(wide?'wide':'')+'""><input data-key=""'+key+'"" type=""checkbox"" '+(v?'checked':'')+'><span>'+label+'</span></label>';var step=type==='number'?' step=""0.01""':'';return '<label class=""'+cls+'""><span>'+label+'</span><input data-key=""'+key+'"" type=""'+(type||'text')+'""'+step+' value=""'+esc(v)+'""></label>'}function inverseCheck(label,key,wide){return '<label class=""check '+(wide?'wide':'')+'""><input data-key=""'+key+'"" data-invert=""1"" type=""checkbox"" '+(!active()[key]?'checked':'')+'><span>'+label+'</span></label>'}function selectField(label,key,items,wide){var v=String(active()[key]);var o=items.map(function(x){return '<option value=""'+x[0]+'"" '+(String(x[0])===v?'selected':'')+'>'+x[1]+'</option>'}).join('');return '<label class=""field '+(wide?'wide':'')+'""><span>'+label+'</span><select data-key=""'+key+'"" data-number=""1"">'+o+'</select></label>'}function color(label,key){var v=Number(active()[key]||7);return '<button class=""field color"" data-color=""'+key+'""><span>'+label+' · ACI '+v+'</span><i class=""swatch"" style=""background:'+aci(v)+'""></i></button>'}function aci(i){return({1:'#f00',2:'#ff0',3:'#0f0',4:'#0ff',5:'#00f',6:'#f0f',7:'#fff',8:'#808080',9:'#c0c0c0'})[i]||'#64748b'}function group(title,content){return '<div class=""group""><h2>'+title+'</h2>'+content+'</div>'}var tab='surface';function active(){return state[tab]}function render(){['surface','pipe','node'].forEach(function(id){document.getElementById(id).hidden=tab!==id});document.querySelectorAll('nav button').forEach(function(b){b.classList.toggle('active',b.dataset.tab===tab)});document.getElementById('surface').innerHTML=group('计算设置',field('边界插值间隔 m','BoundaryInterval','number')+selectField('计算设置','CalculationMode',[[2,'表面积标注'],[3,'面积标注']])+inverseCheck('保留三角网和相关生成对象','DeleteCassGeneratedObjects',true))+group('注记设置',field('注记文字高度 m','TextHeight','number')+field('面积精确位数','DecimalPlaces','number')+field('CAD 字体样式','AnnotationFontName','text',true)+field('注记模板','AnnotationTemplate','text',true)+selectField('注记图层方式','LayerMode',[[0,'默认 ZJ'],[1,'已有图层'],[2,'自定义图层']])+field('已有图层','SelectedLayerName')+field('自定义/默认图层名','AnnotationLayerName','text',true))+'<button class=""primary run"" data-run=""surface"">开始表面积标注</button>';document.getElementById('pipe').innerHTML=field('文字高度','TextHeight','number')+field('小数位数','DecimalPlaces','number')+field('文字样式','AnnotationFontName')+field('注记模板','AnnotationTemplate','text',true)+field('自动注记图层后缀','AutoAnnotationLayerSuffix')+field('回退注记图层','FallbackAnnotationLayerName')+field('绘制底部开挖注记','DrawBottomAnnotation','check',true)+field('底部注记模板','BottomAnnotationTemplate','text',true)+'<button class=""primary run"" data-run=""pipe"">开始注记管线边长</button>';document.getElementById('node').innerHTML=field('文字高度','TextHeight','number')+field('小数位数','DecimalPlaces','number')+field('文字样式','AnnotationFontName')+field('注记图层','AnnotationLayerName')+field('行间距系数','LineSpacingFactor','number')+color('节点号颜色','NodeNoColorIndex')+color('文字颜色','TextColorIndex')+color('预览引线颜色','PreviewLeaderColorIndex')+'<button class=""primary run"" data-run=""node"">开始注记节点</button>';bind()}function bind(){document.querySelectorAll('section [data-key]').forEach(function(e){e.onchange=function(){var k=e.dataset.key;if(e.type==='checkbox')active()[k]=e.dataset.invert==='1'?!e.checked:e.checked;else if(e.type==='number'||e.dataset.number==='1')active()[k]=Number(e.value);else active()[k]=e.value}});document.querySelectorAll('[data-color]').forEach(function(e){e.onclick=function(){post('pickWastewaterAnnotationColor',JSON.stringify({path:e.dataset.color,index:Number(active()[e.dataset.color]||7)}))}});document.querySelectorAll('[data-run]').forEach(function(e){e.onclick=function(){save();var route=e.dataset.run==='surface'?'runWastewaterSurfaceAnnotation':(e.dataset.run==='pipe'?'runWastewaterPipeAnnotation':'runWastewaterNodeAnnotation');post(route,'')}})}function save(){document.querySelectorAll('section:not([hidden]) [data-key]').forEach(function(e){e.dispatchEvent(new Event('change'))});post('saveWastewaterAnnotationSettings',JSON.stringify(state))}document.querySelectorAll('nav button').forEach(function(b){b.onclick=function(){tab=b.dataset.tab;render()}});document.getElementById('save').onclick=save;document.getElementById('defaults').onclick=function(){state={surface:{BoundaryInterval:5,TextHeight:1,DecimalPlaces:2,AnnotationTemplate:'{图层名}表面积：{表面积}㎡',CalculationMode:2,CassSurfaceLogPath:'',DeleteCassGeneratedObjects:true,AnnotationFontName:'宋体',LayerMode:0,SelectedLayerName:'ZJ',AnnotationLayerName:'ZJ',UseBoundaryLayerForAnnotation:false,DrawLeader:true},pipe:{TextHeight:1,DecimalPlaces:2,AnnotationTemplate:'{图层名}长度：{长度}m',AnnotationFontName:'宋体',AutoAnnotationLayerSuffix:'注记',FallbackAnnotationLayerName:'未分类注记',DrawBottomAnnotation:false,BottomAnnotationTemplate:'开挖：长{长度}m、宽{宽}m、高{深}m'},node:{TextHeight:1,DecimalPlaces:2,AnnotationFontName:'宋体',AnnotationLayerName:'ZJ',LineSpacingFactor:1.45,NodeNoColorIndex:1,TextColorIndex:7,PreviewLeaderColorIndex:1}};render()};window.applyPickedColor=function(v){if(!v||!v.path)return;state.node[v.path]=v.index;render()};window.CDBoxStudioToast=function(m){var t=document.getElementById('toast');t.textContent=m||'';t.classList.add('show');setTimeout(function(){t.classList.remove('show')},2200)};render();";
        }

        private sealed class SettingsEnvelope
        {
            public SurfaceAreaAnnotationOptions surface { get; set; }
            public PipeLengthAnnotationOptions pipe { get; set; }
            public NodeAnnotationOptions node { get; set; }
        }

        private sealed class ColorRequest
        {
            public string path { get; set; }
            public int index { get; set; }

            public ColorRequest()
            {
                path = string.Empty;
                index = 7;
            }
        }
    }
}
