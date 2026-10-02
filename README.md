# ROE Fighter（Unity 版）

用 Rise of Eros（ROE）的角色做的 3D 格斗游戏。引擎是 Unity 6000.4.12f1 + URP。原计划用 UFE 2（Universal Fighting Engine 2）做对战框架，暂时买不了，所以先自己写了一套能玩的格斗逻辑（见"格斗"一节）；角色、动作、技能、特效这些数据以后换 UFE 也照样能用。
首发两个角色：a08（Inase）和 g04（Luf）。

从游戏里取出的素材和买来的插件都不进这个仓库（见 `.gitignore`）：`Assets/ROE`、`Assets/RoeFighter/Generated`、`Assets/UFE*`、`_work`、`out`。
这些素材来自商业游戏，只在本机自己用。

## 现状（2026-10-02）

| 内容 | 状态 |
|---|---|
| a08、g04 的高模、游戏材质、动作、音效进 Unity | 完成 |
| 五个角色着色器（服装、皮肤、头发、眼睛、眉毛睫毛） | 完成，算法照游戏编译后的着色器反汇编逐条重写（10-02） |
| 展示视频（两人各播一遍自带的待机、三个技能、受击，带技能音效） | `out\a08_g04_showcase.mp4` |
| Unity 人形骨架 + 自带动作转成人形动作 | 完成，和原动作的平均误差 0.03–1.1 厘米 |
| 互相套用动作（g04 播 a08 的技能等） | 能播；头发裙子没有物理，辅助骨骼还没有驱动 |
| 游戏自己的战斗场景（s01，城堡屋顶） | 完成，烘焙光照原样可用；自动找一条没有遮挡的对战线 |
| 技能表：每个技能的命中时刻、伤害分段、冲刺位移、震屏、特效、音效、语音 | 完成，从游戏的时间轴和技能演出设置里读出来 |
| 场景里的对打演示（按技能表演，带特效、音效、语音，最后一招击倒） | `out\a08_g04_duel_s01.mp4` |
| 技能特效（游戏原本的粒子预制体 + 六个重写的特效着色器 + 游戏原本的时间轴） | 完成：动作和特效都由游戏自己的时间轴驱动，打在对手身上的命中特效按技能表生成 |
| 挑开阔的战斗场景：194 个场景全部粗筛，5 个导入 Unity | 完成；默认用格斗俱乐部擂台 `e23_steel_s02` |
| 基础动作（走、退、跑、刺拳、直拳、踢、挥砍、防御架势）：公开动作捕捉数据转人形 | 完成；侧步改成程序化迈步（两节骨 IK，脚不滑） |
| 手臂和腿：皮肤挂在辅助骨上，从游戏动作里拟合出辅助骨怎么跟随肢体，动捕动作时实时驱动；动捕着地；走路不滑；高跟鞋站稳（鞋跟和前掌都着地） | 完成（10-02 下午，高跟鞋 10-02 晚），见"手臂和腿"一节；对比图 `out\feet_fix_1002.jpg` |
| 布料：裙子、头发、链子、胸的骨骼布料（移植自 bone_cloth 插件，照 Magica Cloth 2） | 完成，基础动作时生效；对比视频 `out\bone_cloth_demo.mp4`；10-02 晚裙子按游戏站姿垂下（之前翘出 45°），对比图 `out\skirt_fix_1002.jpg` |
| 动作包：基础动作可以整套替换（F3 切换，买来的人形动作写个 JSON 就能导入） | 两个包：`bandai1`（默认）和开源动捕做的 `accad_male2`（拳击架势、步法、前后腿回旋踢，10-02 晚）；对比视频 `out\motion_packs_demo.mp4`，用新包的整场对打 `out\fight_cpu_match_accad.mp4` |
| 能玩的格斗：移动、侧步、四个攻击键、防御、受击、倒地、三个技能（含超必杀）、能量槽、回合、计时、HUD、电脑对手、场地自带的战斗音乐 | 能玩：`out\ROEFighter\ROEFighter.exe`（10-02 打包），或在编辑器里打开场景按 Play；电脑对电脑的录像 `out\fight_cpu_match_1002b.mp4` |
| UFE 对战 | 暂时买不了；先用自己的格斗逻辑 |

## 目录

```
Assets/RoeFighter/Shaders/     角色、场景、特效着色器和公共代码
Assets/RoeFighter/Runtime/     运行时也要用的代码：技能表的数据结构，Fight/ 下是格斗逻辑
Assets/RoeFighter/Editor/      编辑器脚本：建工程设置、拼角色、拍图、转动作、场景、对打演示、各种检查
Assets/RoeFighter/Settings/    渲染管线、影棚的后期和地面
Assets/ROE/                    （不入库）从游戏取出的素材，按角色和游戏包分文件夹
Assets/RoeFighter/Generated/   （不入库）拼好的角色预制体、人形骨架、转好的动作、mocap/ 下是动作捕捉转成的基础动作
Assets/RoeFighter/Scenes/      （不入库）生成的对战场景（游戏的战斗场景 + 两个角色 + 格斗逻辑）
tools/                         Python / PowerShell 工具
_work/                         （不入库）游戏包的副本、提取结果、日志、渲染帧
out/                           （不入库）给人看的视频和图
```

## 从零重做一遍

```powershell
cd E:\code\othercode\roe_fighter_unity

# 1. 把两个角色用到的游戏包拷到 _work\bundles（94 个，约 430 MB）
python tools\stage_bundles.py _work\bundles a08 g04

# 2. 另开一个窗口启动 AssetRipper 并留着它，然后回到这个窗口导出成 Unity 工程
#    （另一个窗口）E:\tools\AssetRipper_1.3.14\AssetRipper.GUI.Free.exe --headless --port 5599 --log-path _work\assetripper.log
python tools\rip.py          # 设置 → 读入（缺的依赖包自动补）→ 导出到 _work\ripped，约半分钟

# 3. 拷进工程，材质改指向这里的着色器，写出清单
python tools\import_ripped.py

# 4. Unity 里的步骤（都是命令行，不用开编辑器）
.\tools\unity_batch.ps1 -Method RoeFighter.EditorTools.RoeProjectSetup.Run -Graphics        # 渲染管线，只需一次
.\tools\unity_batch.ps1 -Method RoeFighter.EditorTools.RoeShaderCheck.Run -Graphics         # 编译全部着色器变体
.\tools\unity_batch.ps1 -Method RoeFighter.EditorTools.RoeFighterBuilder.BuildAll           # 拼角色预制体
.\tools\unity_batch.ps1 -Method RoeFighter.EditorTools.RoeShowcase.Stills -Graphics         # 每人四张定妆图
python tools\make_sheet.py
.\tools\unity_batch.ps1 -Method RoeFighter.EditorTools.RoeShowcase.Video -Graphics          # 渲染视频帧
python tools\make_video.py                                                                  # 合成 mp4，配技能音效
.\tools\unity_batch.ps1 -Method RoeFighter.EditorTools.RoeHumanoidClips.ConvertAll          # 人形骨架 + 转动作 + 误差报告
.\tools\unity_batch.ps1 -Method RoeFighter.EditorTools.RoeShowcase.RetargetStills -Graphics # 互相套用动作的检查图
```

`unity_batch.ps1` 跑完会把日志里带 `[ROE]` 的行和报错打印出来，完整日志在 `_work\logs\`。
`-Graphics` 表示要用显卡（渲染、编译着色器时需要）。同样的功能在编辑器菜单 **ROE Fighter** 下也有。

## 每一步是怎么做的

### 素材：直接取 Unity 原生资源

游戏是 Unity 2022.3 做的，所以不走 FBX，用 AssetRipper 把游戏包还原成 Unity 自己的资源：预制体、网格、材质、贴图、动作、音频都是原样，没有任何格式转换的损失。

- 一套服装涉及的游戏包（`stage_bundles.py` 里的规则）：
  - `chara_armor_pc_<id>_hd & ld_hd`：高模预制体、展示动作（idle_02、react_01/02）、武器。
  - `chara_armor_pc_<id>_hd & ld_ld*`：战斗用低模 + **战斗动作**（idle_01、skill_01..03、hurt、die、rip）。
  - `meta_armor_pc_<id>_hd & ld`：展示用的完整角色，**每个网格该用哪个材质**只有这里有，另外还带游戏自己的布料物理设置。
  - `chara_mat_*`、`chara_tex_*`：材质和贴图；头部、公共贴图在 `…_common_*` 包里。
  - `sfx_battlefield_pc_<id>*`、`voice_battlefield_<语言>_pc_<家族>01*`：技能音效和语音。
- 高模预制体上挂的是占位材质，`RoeFighterBuilder` 按网格名从 meta 预制体里把真材质抄过来。
- a08 的高模没有武器网格，战斗低模里有（顶点数和高模武器相同，绑在同名骨骼上），拼角色时接到高模骨架上。
- 包和包之间靠内部编号（CAB）引用。编号不是文件名的哈希，`cab_index.py` 读每个包的文件头建索引；`ar_load.py` 看 AssetRipper 的日志，缺哪个补哪个。
- 游戏的展示视频（`gacha_video_gacha_special_ssr_pc_<id>.ab`）是游戏自己渲染的画面，`tools\extract_video.py` 能取出来，调材质时当参照。

### 着色器

AssetRipper 还原不出游戏着色器的源码，只给参数表（名字、范围、开关）。这里的着色器**参数名和游戏的一样**，所以游戏的材质文件原样能用，`import_ripped.py` 只把材质里的着色器引用换成这里的。

源码虽然没有，**编译后的程序是有的**：`tools\shader_asm.py` 从 `heros_shader_collection.ab` 里取出每个着色器、每种开关组合的 DirectX 11 程序，用 Windows 自带的 `d3dcompiler_47.dll` 反汇编，并把常量缓冲区的布局（哪个偏移是哪个材质参数）、贴图槽位、混合和深度状态一起写成文本（`_work\shader_asm\`）。照着它就能把算法一行一行对出来。特效着色器（见"技能"一节）和五个角色着色器都是这样重写的。
`tools\mat_params.py a08 g04 [--shader ROE/Skin] [--props _BumpScale,...]` 列出角色实际用到的材质参数，用来判断哪些差别真的看得出来。

```powershell
python tools\shader_asm.py                                   # 列出游戏的全部着色器
python tools\shader_asm.py "Pinkcore/Particles"              # 名字里含这段的都导出
python tools\asm_variant.py _work\shader_asm\Pinkcore_Particles_UnlitMaster.txt f "DISSOLVE DISTORTION _COLORMODE_MULTIPLY _EMISSIONMODE_SELF"
```

| 着色器 | 对应游戏的 | 要点 |
|---|---|---|
| `ROE/Character` | ErosLit/Character | 就是 URP 标准的 PBR：主光 GGX、球谐环境光、反射探针（都乘 MGA 的遮蔽），点光聚光按顶点。另加：细节法线用 RNM 混合；掠射角的反射乘 `_FresnelStrength`；最终颜色在边缘按 `(1-N·V)^4 × _RimColor.a` 插值到 `_RimColor`（受击闪光用）。底色图用 `_BaseMap_ST`，法线、MGA、自发光用 `_UVScaleOffset`，细节法线用原始 UV × `_DetailNormalMap_ST` |
| `ROE/Skin` | Skin | 漫反射 = 游戏的皮肤 LUT（横轴：模糊法线的半兰伯特；纵轴：MGAC 的 A × `_LutCurveStrength`）×（底色 + 透光色）。模糊法线是法线图往模糊方向偏 `_BumpBias` 级 mip，不带细节和强度。"透光"其实是越靠轮廓越强的红色：`(0.04 + 0.96 (1-N·V)^_TranslucentPower) × _TranslucentColor × MGAC.a`，跟着 LUT 和主光走。高光是 GGX，但乘的是模糊法线的 N·L。环境反射不加掠射增强 |
| `ROE/Hair` | KajiyaKayHair | 漫反射 = 底色 × 0.954 × lerp(N·L, 1 − abs(发丝·L), 0.33)；两层 Kajiya-Kay 高光都乘 `0.5 × (1 − 0.954 × 底色)`（深色头发反而更亮）和 N·L。环境光是把视线方向当光源代进同一个公式再乘球谐，发丝遮蔽只作用在环境光上。游戏分"深度预写 + 不透明 + 半透明边缘"三遍画；这里是一遍按不透明裁切画（配合 MSAA 抗锯齿），能投影、能参与景深 |
| `ROE/Eye` | Eye | 在眼球 UV 圆盘里合成。虹膜范围是遮罩图的 R；虹膜贴图按 `lerp(_PupilMinSizeInv, _PupilMaxSizeInv, _PupilSizeScale) × _IrisLimbusScaleInv` 缩小采样（贴图里的小瞳孔就是这样放大的），再按遮罩图 G 通道的深度做视差；角膜缘是按枢轴半径算出的一圈，颜色直接替换。漫反射用眼球自身的法线；高光是 GGX，但在虹膜上把 N·H、L·H 重新拉伸成一个清晰的圆斑，并封顶 `_SpecularTermMax`；反射按 4% 起、到边缘升到掠射值，与金属度无关。遮蔽图是**白色 = 被眼睑挡住** |
| `ROE/Eyebrow` | Eyebrow | 底色 × 0.96 × (主光 N·L × 阴影 + 球谐 + 顶点点光)，没有高光；材质既预乘 alpha 又按 SrcAlpha 混合（颜色 × alpha²），所以眉毛睫毛的软边偏深、显得浓 |

和游戏不同的地方（都是有意的）：
- **瞳孔大小**：两个角色的眼睛材质里 `_PupilSizeScale` 是 0.56（a08）和 0.76（g04），按游戏的公式瞳孔会占可见虹膜的 45–60%；而游戏自己渲染的展示视频里，瞳孔正好是这个值取 0 时的大小，说明游戏运行时会改它。`RoeFighterBuilder` 拼角色时把它设为 0（日志里有一行）。
- 眼球网格的切线不可靠，眼睛着色器用屏幕空间导数自己算切线（游戏用网格切线）。
- 头发的画法见上表；场景里的额外方向光（影棚的补光、轮廓光）逐像素算，游戏里没有这种光。

踩过的坑：
- 眼球网格的切线不可靠，用它算视差会让整只眼睛变黑。眼睛着色器改用屏幕空间导数自己算切线。
- 虹膜贴图里的瞳孔只有虹膜的 12%：游戏是把虹膜贴图缩小采样（等于放大）得到正常大小的，见上表。
- 这个版本的 URP，`UnpackNormalScale` 是先由未缩放的 xy 算出 z、再缩放 xy（z 不变），和游戏头发着色器的做法一样；
  游戏的服装、皮肤着色器则是把整个法线向 (0,0,1) 插值（`RoeCore.hlsl` 的 `RoeUnpackNormalLerp`）。
- 游戏的眼睛遮蔽图是白色表示被挡住（`1 − 贴图 × 强度`），和常见的 AO 图正好相反。
- 换新材质后的第一帧会用占位着色器渲染（皮肤发紫、衣服不见）。拍图前先空渲两帧。
- Unity 的方向光强度 1 就是"白色表面输出 1"，主光给 2 以上皮肤直接过曝。

### 展示视频

`RoeShowcase.Video` 不进播放模式，逐帧把动作采样到角色上再渲染，所以结果每次都一样。
镜头先扫一遍整段动作里骨骼（含武器）的范围，再做带"预知"的平滑：起跳、突进之前镜头就开始动，人不会出画。
`make_video.py` 按 `timeline.json` 把每个技能对应的游戏音效叠上去。

### 人形骨架和动作转换（给 UFE 用）

UFE 自带的格斗动作是 Unity 的人形动作，要让 ROE 角色能用，角色得有人形骨架。游戏里这些角色是普通骨架（3ds Max Biped）。

- `RoeHumanoid.BuildFighter`：
  - 按 Biped 的骨骼名对应到 Unity 的 52 根人形骨骼；
  - 把四肢摆直成 T 姿势（原始姿势肘部弯 11–17 度），脚保持高跟姿势不动，这样平底的动作套上来脚还是踩着高跟；
  - 有的服装大腿挂在脊柱下、锁骨挂在脖子下（g04 就是），人形骨架骨长固定，这种挂法脊柱一弯髋关节就跟着跑。做法是把这四根骨骼改挂到骨盆和上胸（世界姿势不变，蒙皮不受影响）；
  - 肢体扭转全部放在下一级关节（`upperArmTwist` 等四项设 1）。这些骨架有自己的扭转辅助骨骼，用 Unity 默认的 0.5，辅助骨骼会偏 7–13 厘米；
  - 武器改挂到待机时离它最近的骨骼上（g04 的扇子挂右手；a08 的剑待机时插在地上，挂到了骨盆），其余非人形骨骼的默认值取待机第一帧（g04 的扇子在原始姿势是完全展开的巨大状态，待机里才收起缩小）。
  - 输出 `<id>_fighter.prefab` 和 `<id>_humanoid_avatar.asset`；原来的 `<id>.prefab` 不变。
- `RoeHumanoidClips.ConvertAll`：把游戏的普通动作转成人形动作。
  - 在原骨架上采样原动作，把姿势按骨骼名抄到 fighter 骨架上，用 `HumanPoseHandler` 读出肌肉值和根运动，每帧一个关键帧；
  - 头发、裙子、胸、扭转辅助骨骼、脸这些非人形骨骼保留原来的曲线；
  - 骨盆以上的节点（Root、Bip001）不再带曲线，挂在它们下面的东西和改挂过的武器按新父级重算曲线；
  - 转完自动和原动作逐帧比对所有蒙皮骨骼的位置，日志里每个动作一行。目前平均 0.03–1.1 厘米，最差 1–3 厘米（都在扭转辅助骨骼上）。

### 战斗场景（游戏自己的舞台）

游戏有 194 个战斗场景（`scene_battlefield_*.ab`），每个只有几十 KB，引用的模型、贴图、光照图在别的包里。

```powershell
# 取一个场景：只放场景包，依赖的包（这个场景是 107 个、76 MB）会自动补齐
mkdir _work\bundles_stage_s01
copy "<游戏>\RiseOfEros_Data\StreamingAssets\AssetBundles\scene_battlefield_level000_s01_day.ab" _work\bundles_stage_s01\
python tools\rip.py --bundles _work\bundles_stage_s01 --out _work\ripped_stage_s01
python tools\import_stage.py s01                                   # → Assets\ROE\stages\s01
.\tools\unity_batch.ps1 -Method RoeFighter.EditorTools.RoeStage.Report -Graphics   # 场景里有什么
.\tools\unity_batch.ps1 -Method RoeFighter.EditorTools.RoeStage.Stills -Graphics   # 两人站上去拍四张
.\tools\unity_batch.ps1 -Method RoeFighter.EditorTools.RoeDuel.Run -Graphics       # 轮流放技能的演示帧（要先有技能表，见下一节）
python tools\make_video.py _work\duel _work\duel\duel.mp4
python tools\scene_tree.py <场景.unity> [--scripts]                                # 不开 Unity 看场景层级和游戏脚本的数据
```

- 场景文件、烘焙光照图（2 张）、光照探针、雾、天空盒、反射贴图都能原样带过来，Unity 打开场景后光照图自动生效。
- 新写了两个着色器：`ROE/Environment`（和角色服装同一套表面模型，加上光照图）和 `ROE/Skybox`（立方体贴图 + 地平线雾）。
- `RoeStage.Open` 会去掉工程里没有的游戏脚本、关掉游戏自己的相机、隐藏没有替代着色器的东西（这个场景是 11 个粒子特效）。
- 场景里的 `BattleFieldSetting/FormationSetting` 是游戏的站位：`Center` 是中心，双方各在中心线两侧 3 米。
- **对战线怎么选**（`RoeStage.FindLayout`）：游戏是回合制，双方沿场景纵深面对面站，镜头从己方身后看过去；格斗游戏要从侧面看，
  照搬游戏的站位，镜头和人之间全是场景里的尖塔、箱子。做法是给场景网格临时加碰撞体，对游戏战斗中心附近的每个位置、每个朝向做射线检测：
  脚下 2.4 米宽的地面要平、上面不能有东西，镜头在几种距离上到对战线各点的视线都不能被挡。先按 12 米长的对战线找，找不到再缩短。
  s01 的结果是：就在游戏的战斗中心，镜头顺着红毯看向城门（正是游戏自己的视角方向），两人横在红毯上，对战线 7 米。
- **朝向**：游戏的动作是按"模型根节点正对敌人"做的，站姿本身侧身（胯部转 20–40 度）。按脚尖方向摆会让上身和脸偏开对手。
  侧身只在屏幕一侧是正面朝镜头，另一侧的角色整体左右镜像（格斗游戏的常规做法），`RoeStage.Place` 自动判断。
- `RoeDuel.Run` 出片前会沿实际镜头轨迹再做一遍视线检查，日志里报告有多少帧被场景挡住。这段演示不是对战，没有判定和操作。

### 技能：按游戏自己的数据演

游戏里每个单位（一套服装）有一个玩法预制体 `gameplay_prefab_<单位>.ab`（a08 是 `suit_ines_8`，g04 是 `suit_luf_4`），
一个技能怎么演全在里面：

- 每个动作（待机、三个技能、受击、倒地……）是一条 Unity **时间轴**：哪一秒播哪个动作片段、哪一秒打开哪个特效、哪一秒放哪个音效和语音。
- 时间轴之外的由 `CharacterSkillView` 组件规定：**伤害数字出现的时刻和每段的比例**（a08 的 1 技能是 0.85 / 2.7 / 2.9 秒三段）、
  角色什么时候冲到对手面前（离对手 2 米 = 自己的攻击半径 + 对手的受击半径）又什么时候回去、震屏的时刻和强度、技能专用镜头、打在对手身上的命中特效。
- 伤害公式、目标选择这些逻辑在另一处：`%USERPROFILE%\AppData\LocalLow\Pinkcore\Rise of Eros\GameplayAssets\gameplayAsset.data`
  （982 个 JSON / Lua 文件，`tools\gameplay_unpack.py` 解包）。演出时间不在这里。

```powershell
python tools\cab_index.py build _work\cab_index.json          # 只需一次
python tools\skill_sheet.py suit_ines_8 a08 suit_luf_4 g04    # → Assets\ROE\<id>\skill_sheet.json 和 skill_sheet_unity.json
python tools\timeline_dump.py suit_ines_8 --root suit_Ines_8  # 只想看时间轴
python tools\bundle_dump.py gameplay_prefab_suit_ines_8 --list # 看一个包里有什么

# 特效资源：取玩法预制体，依赖会补齐到约 900 个包（共享设置里挂着全游戏的增益特效），导入时只拷这两个单位用得到的 334 个文件
mkdir _work\bundles_skills
copy "<游戏包目录>\gameplay_prefab_suit_ines_8*.ab" _work\bundles_skills\
copy "<游戏包目录>\gameplay_prefab_suit_luf_4.ab" _work\bundles_skills\
python tools\rip.py --bundles _work\bundles_skills --out _work\ripped_skills --log _work\assetripper_skills.log
python tools\import_skills.py suit_ines_8 suit_luf_4           # → Assets\ROE\skills
.\tools\unity_batch.ps1 -Method RoeFighter.EditorTools.RoeShaderCheck.Run -Graphics
.\tools\unity_batch.ps1 -Method RoeFighter.EditorTools.RoeSkillTest.Run -Graphics -Extra '-roeChar','a08','-roeAction','skill1'
```

- **技能表**（`skill_sheet.py`）：把上面这些整理成每个单位一份 JSON，`RoeSkillSheet`（Runtime）读它。对打演示和以后 UFE 的招式数据都从这份表来。
- **导入**（`import_skills.py`）要改写三种引用：
  - AssetRipper 每次导出都给资源新编号，工程里已有的资源（角色模型、音效）按"包名 + 包内路径"对回原来的编号，不重复拷；
  - Timeline、URP 这些 Unity 官方包的脚本，AssetRipper 导出的是空壳，改指向 `Library\PackageCache` 里的真脚本——这样游戏的时间轴文件在这里原样能用；
  - 游戏的着色器换成这里重写的。
- **特效着色器**：六个，都是照反汇编重写的，参数名不变。

| 着色器 | 对应游戏的 | 做什么 |
|---|---|---|
| `ROE/Particles/Default` | Particles/Default | 贴图 × 顶点色 + 自发光；UV 滚动量来自粒子的自定义数据 |
| `ROE/Particles/Dissolve` | Particles/Dissolve | 顶点透明度当溶解进度，按溶解图裁掉 |
| `ROE/Particles/Unlit` | Particles/Unlit | 精简的 URP 粒子着色器：六种和顶点色的混合方式、软粒子、近镜头淡出、序列帧混合 |
| `ROE/Particles/UnlitMaster` | Particles/UnlitMaster | 新的全能版：UV 偏移可给底图 / 溶解图 / 扭曲图，带发光边的溶解，菲涅尔发光，混合方式由材质定 |
| `ROE/Particles/Distortion` | Particles/Distortion | 把身后的画面按法线图推歪（热浪、冲击波） |
| `ROE/Particles/MutateDistortion` | Particles/MutateDistortion | 贴图盖在扭曲过的背景上，可溶解 |

  游戏的两个扭曲着色器在一个单独的阶段画，读的是"已经画完其它特效的画面"；这里读的是 URP 的不透明画面（不含别的特效），所以渲染管线开了 Opaque Texture。
- **播放**（`RoeUnit`）：直接用游戏的单位预制体和它的时间轴。时间轴驱动预制体里的战斗低模，特效挂点有的就在低模的骨骼上；
  低模留着但不画（"替身"），我们的高模每帧按骨骼名抄它的姿势（只抄有动作曲线的骨骼，其余保持高模自己的值）。
  不进播放模式，逐帧 `director.time = t; director.Evaluate()`，粒子由时间轴按时间推进，每次渲染结果一样。
- **对打演示**（`RoeDuel.Run`）用的就是 `RoeUnit`：攻击方播技能的时间轴，冲刺和回位、命中时刻、震屏、音效语音按技能表；
  技能表里"生成在目标身上"的命中特效（g04 的每一下、a08 的 2、3 技能）在对手的挂点上实例化，有自己时间轴的按时间轴推进，没有的直接推粒子。
  `-roeFrames 110-330` 只渲染一段（前面的帧照样计算，粒子状态才对），`-roeSize 960x540` 出小图，`-roeProbe 110,126` 在日志里打印这几帧的骨骼位置。
- `RoeSkillTest.Run`：在影棚里单独播一个技能出几张图；`-roeDump 2` 列出第 2 张图时所有在画的东西和灯，并分别出"只有特效""没有特效"两张；
  `-roeCompare 0.72` 把时间轴给的姿势和直接采样动作片段的姿势逐骨骼比对；`-roeMirror 1` 看镜像后的样子。

踩过的坑（这四个都是看渲染结果、再一层层查出来的）：

1. **特效全挂在世界原点 / 根本不出来。** 时间轴片段用"暴露引用"的名字找场景里的挂点，表在 `PlayableDirector` 上。
   打包后的游戏只保留名字的哈希（Unity 的 `PropertyName` 就是名字的 CRC32），AssetRipper 把这个数字当名字写回去，两边对不上。
   `import_skills.py` 把两边都改成 `roe_<数字>`，并且**按新名字的 CRC32 重新排序** director 上的表——Unity 用二分查找，不排序只能找到一部分。
2. **地面被打得一片橙黄。** 命中特效的粒子带"灯光"模块（亮度 8.3、范围 10 米、最多 15 盏）。照 URP 默认的逐像素算，整块地面都被照亮。
   反汇编显示游戏的着色器只有 `_ADDITIONAL_LIGHTS_VERTEX` 一种：点光、聚光按顶点算，朴素的 Lambert，衰减封顶 1。
   现在角色和场景着色器都照这样算点光、聚光（`RoeCore.hlsl` 的 `RoePunctualVertexLights`），影棚里额外的方向光（补光、轮廓光）仍逐像素。
3. **头和身体错位（像头被打飞）。** 一帧编辑器更新里连续渲染很多帧时，蒙皮网格只在本次更新第一次渲染时算骨骼矩阵，之后一直用旧的，
   于是同一张图里头是这一帧的姿势、身体是另一帧的。给单位下所有 `SkinnedMeshRenderer` 设 `forceMatrixRecalculationPerRender = true`。
   （之前用 `AnimationMode` 采样的版本没碰到，是因为那条路每次采样都会让渲染器重新蒙皮。）
4. 游戏预制体里 Timeline、URP 的脚本必须指向 `Library\PackageCache` 里的真脚本（见上面的"导入"），否则时间轴根本加载不起来。

### 开阔的战斗场景：先粗筛，再挑

s01 只有 7 米宽，做不了能绕圈的 3D 格斗。游戏有 194 个战斗场景（白天、黄昏、夜晚版本几何相同，只算一次，共 168 个），全部导出太慢，所以先粗筛：

```powershell
python tools\stage_survey.py                 # 约 2 分钟：_work\stage_survey\survey.json + 每个场景一张俯视空地图
python tools\stage_survey.py level002 anni   # 只看名字里含这些的
```

- 直接用 UnityPy 读场景包和它引用的网格（不经过 AssetRipper），把每个显示中的网格的三角形按 0.15 米间距采样，放进战斗中心周围 ±20 米、0.25 米一格的俯视网格：
  在战斗中心高度 ±0.2 米、朝上的面算"地面"，0.25～2.2 米高度有东西算"挡住"（墙、柱子、台阶、栏杆）。
- 算出能放下的最大空圆（中心可以偏离战斗中心 4 米以内），俯视图里灰色是空地，红色是障碍，绿圈是最大空圆。
- 结果（半径）：level003_s03 12.6 米、level002_s01 12.2 米、2026_anni_s01 12.0 米……s01 只有 3.5 米。

挑了 5 个导入（`tools\rip_stages.ps1 -Stages '简称=场景名',...`，每个半分钟到两分半；简称和场景名的对照写进 `_work\stage_survey\stages.json`）：

| 简称 | 场景 | 样子 |
|---|---|---|
| `e23_steel_s02` | level_event_2023_realsteel_s02 | **格斗俱乐部擂台**，地上写着 G.O. F.I.G.H.T.，有比分牌和铁栅围栏，半径 8 米。默认用这个 |
| `l003_s03` | level003_s03 | 白色大理石的奇幻广场，最亮最漂亮，半径 12.6 米 |
| `e24_anni_s01` | level_event_2024_anni_s01 | 大理石圆形竞技场，外圈红环，半径 8.2 米 |
| `l002_s01` | level002_s01 | 夜里的日式神社圆形场地（灯笼、旗幡、锁链），偏暗 |
| `e26_anni_s01` | level_event_2026_anni_s01 | 宫殿前的大广场，地面单调 |

`RoeFightScene.Build` 用粗筛的结果定场地中心和半径（射线检测会从细栏杆的缝里漏过去，所以粗筛更准）；没粗筛过的场景再退回到射线检测。

### 基础动作：动作捕捉数据

游戏的角色只有待机、三个技能、受击、倒地，没有走路和拳脚。基础动作用的是公开的动作捕捉数据：
**Bandai Namco Research Motion Dataset 1**（Bandai Namco Research Inc.，CC BY-NC 4.0，非商用、需署名），
https://github.com/BandaiNamcoResearchInc/Bandai-Namco-Research-Motiondataset 。BVH 格式，30 fps，22 个关节，没有手指。

```powershell
python tools\fetch_mocap.py                  # 35 段 BVH + 标注，约 7 MB，存到 _work\mocap\bandai1
python tools\bvh_preview.py _work\mocap\bandai1\dataset-1_kick_normal_001.bvh --out sheet.png   # 火柴人连环图 + 手脚速度
.\tools\unity_batch.ps1 -Method RoeFighter.EditorTools.RoeMocap.ImportAll            # → Generated\mocap\mc_*.anim + mocap_strikes.json
.\tools\unity_batch.ps1 -Method RoeFighter.EditorTools.RoeMocap.Preview -Graphics    # 两个角色做每个动作的检查图
```

- 用到的段落：`punch_normal_001` 前手刺拳（也取它开头的拳击架势当防御姿势）、`punch_normal_002` 后手直拳、`kick_normal_001` 回旋踢、
  `slash_normal_001` 挥砍，走、退、跑用 feminine（女性化）风格。
- 转换器 `RoeMocap` 现在是通用的（骨骼名自动识别、单位自动判断），别的 BVH 数据写一个动作包 JSON 就能用，见"动作包"一节的第二个包。
- **T 姿势**：BVH 的零姿势是"每根骨头都沿自己的 x 轴"，不能直接建人形骨架。做法是取一帧自然站立，把骨盆转正，再把每根骨头用最小摆动
  （不加扭转）转到 T 姿势方向——脊柱朝上、手臂水平朝外、腿朝下、脚尖朝前——然后用这个姿势建 Unity 的人形映射。
- 每帧把 BVH 摆到骨架上（右手系转左手系：x 取反，绕 y、z 的角度取反，厘米变米），用 `HumanPoseHandler` 读出肌肉值写成人形动作。
- **对齐**：出招的段落按"出手方向"转到正前方（击打部位伸得最远时，从骨盆指向它的水平方向），拳击架势和刺拳用同一个转角；
  走、退、跑按整段的平均朝向；循环动作在指定范围里找首尾最像的两帧切出一个周期，并把匀速前进的位移从片段里扣掉，原地踏步（位移由格斗逻辑给）。
- **招式表** `mocap_strikes.json`：每招的击打骨骼、最远够到哪（刺拳 0.67 米、直拳 0.92 米、踢 1.04 米、挥砍 0.76 米）、出手高度、
  有效时间段（击打部位在最远距离 85% 以内的那段）。
- **侧步**：数据集里的 walk-left/right 是身体转 70 多度往侧面走，不是面朝前侧跨步。先试过以拳击架势为底生成侧步片段
  （`side_left/right`，两腿交替外展抬腿，还在 `Generated\mocap` 里），但片段的步幅追不上格斗逻辑移动身体的速度，脚在地上滑 1.5 米/秒；
  现在侧步时上半身用拳击架势，腿由代码迈步（见"手臂和腿"一节）。

### 格斗：能玩的版本（UFE 到之前）

```powershell
.\tools\unity_batch.ps1 -Method RoeFighter.EditorTools.RoeFightScene.Build -Graphics [-Extra '-roeStage','l003_s03']   # → Assets\RoeFighter\Scenes\Fight_<场景>.unity
.\tools\unity_batch.ps1 -Method RoeFighter.EditorTools.RoeFightScene.Record -Graphics -Extra '-roeSeconds','150'       # 电脑对电脑，出帧和 timeline.json
python tools\make_video.py _work\fight _work\fight\fight.mp4
.\tools\unity_batch.ps1 -Method RoeFighter.EditorTools.RoeFightScene.BuildPlayer -Graphics                             # → out\ROEFighter\ROEFighter.exe
```

双击 `out\ROEFighter\ROEFighter.exe` 就能玩（窗口 1600×900，可以拉大；整个文件夹 703 MB，打包约 4.5 分钟，不能单独拷 exe）。
也可以在编辑器里打开 `Assets\RoeFighter\Scenes\Fight_e23_steel_s02.unity` 按 Play。默认 1P 是人、2P 是电脑。
第一次打包失败过一次：Unity 自带的 `MovedFromExtractor` 处理 `UnityEngine.IMGUIModule.dll` 时退出码 3、没有任何输出，原样重跑就成功了。
按键：

| | 1P | 2P |
|---|---|---|
| 左右移动（朝对手走 / 后退，按住后退 = 防御） | A / D（或手柄摇杆） | ← / → |
| 侧步（往画面里 / 往画面外，绕着对手转） | W / S | ↑ / ↓ |
| A–D 四个攻击（`bandai1`：刺拳、挥砍、直拳、回旋踢；`accad_male2`：刺拳、前腿回旋踢、直拳、后腿回旋踢） | J、K、U、I（手柄 0–3） | 小键盘 1、2、4、5 |
| 换动作包（比赛重新开始） | F3 | |
| 技能 1、技能 2、超必杀（要满能量槽） | L、O、P，或 236+J/U、214+J/U、236236+J/U | 小键盘 3、6、9 |
| 电脑接管 1P / 2P | F1 / F2 | |

结构（`Assets\RoeFighter\Runtime\Fight\`）：

- `FighterRig`：一个角色的"身体"。人形高模由一个可播放图驱动，分三层：
  0 层永远播游戏的战斗待机（头发、扇子、裙子、武器这些非人形骨骼的值从这里来）；1 层是动作捕捉的基础动作（只动人形骨骼）；
  2 层是游戏自己的动作（受击、倒地、技能、开场和胜利姿势，所有骨骼都动），播它们时整层淡入盖住下面。
  每层里的片段权重每步都写进混合器并归一化（见下面"踩过的坑"第 5 条）。姿势算完之后，按动捕层的权重再做三件事：
  动捕着地、侧步时腿自己迈步、驱动肢体辅助骨（见"手臂和腿"一节）。
  游戏的单位预制体跟在同一个位置，放技能时它的时间轴和高模同步推进，特效挂在它隐藏的低模骨骼上，出现在游戏原本的位置；
  技能在最后一下伤害 0.8 秒后就放开身体（淡回架势 0.3 秒），时间轴在后台播完，特效自然消失。
- `Fighter`：状态机。中立（待机、走、侧步，总是面向对手）、出招（出招时朝向锁定，侧步能躲开）、防御、受击、倒地起身、技能、KO、胜负。
  打中或被挡住之后，从招式的取消点起可以接下一招。技能按技能表冲到对手面前、在游戏规定的时刻造成伤害（按游戏的比例分段）。
- `FightGame`：固定每秒 60 步。每步：输入（键盘、手柄或电脑）→ 两个角色的状态机 → 摆姿势 → 命中判定（击打骨骼的球体对对手身体的竖直胶囊）
  → 技能伤害 → 特效 → 回合规则 → 镜头。打中有 4–6 帧的顿帧和小幅震屏；KO 时慢动作。三局两胜，每局 60 秒。
  技能的第一下决定结果：对手按住后退就是挡住（只掉 10%），走出了范围就是打空，否则命中；挡住或命中后对手被锁住直到技能结束（游戏的演法）。
- `FightAI`：电脑对手每 0.1–0.25 秒想一次：保持距离、按距离挑招、看到对方出招时有一定概率防御或侧步、偶尔放技能、满槽放超必杀。
- `FightHud`：血条（掉血有红色的滞后条）、计时、回合标记、能量槽、中间的大字（ROUND 1 / FIGHT! / K.O. / 谁赢）。录像时由镜头画进画面。
- `RoeFightProbe`（编辑器）：查问题用的几个探针——每个角色在画什么、时间轴绑定在哪、同一时刻"可播放图"和"直接采样"的姿势对比、技能全程的腿长；
  `MatchAt` 按录像同样的种子和步数重放比赛到指定的步，打印混合器里每个片段的实际权重、蒙皮后掉到脚下面的顶点和它们挂的骨头，并拍图；
  `MovesSheet` 拍两个角色每个基础动作关键时刻的侧面和正面特写；`FootSlide` 测走路和侧步时着地的脚滑多快。
- 声音：普通攻击用每个角色最短的那条技能命中音效（之前取到的第一条偏重）；比赛时循环播放场地自带的战斗音乐（格斗俱乐部是
  `bgm_event_realsteel2023_battle`），录像时写进 `timeline.json` 的 `music`，`make_video.py` 循环混在音效下面。

踩过的坑：

1. **扇子在出拳时张开成巨大的扇面。** 动作捕捉的片段没有扇子骨骼的曲线；如果和游戏的片段放在同一个混合器里，混合器认为扇子"被动画了"，
   动捕片段那一份就取默认值（绑定姿势，也就是完全张开的扇子）。所以游戏片段单独放一层，不播游戏片段时那一层权重为零。
2. 游戏的技能动画很长（3–7 秒），最后一下之后还有收招和回原位（1–5 秒）；照原样播，放技能的人这段时间无敌，对手却已经能动。
   现在最后一下 0.8 秒后就结束（技能的回原位滑步若在最后一下 1.5 秒内结束，就等它滑完），对手被锁住的时间按这个结束时刻算。
3. 数据集的 walk-left/right 不是侧步（见上一节）。
4. 每次开机后，命令行跑 Unity 要先把 Unity Hub 开着，否则许可证拿不到（"Access token is unavailable"）。
5. **从第二回合起，g04 放技能时浮在半空，小腿变成一直拖到地面的尖刺。** 每回合开始时用"淡入时间 0"切到开场动作，
   旧代码只改了权重数组、没把归零的权重写进混合器，下一步 `Tick` 又跳过权重已经为 0 的片段——上一回合的胜利动作 `idle_02`
   就以权重 1 一直留在游戏动作层里。人形混合器不会归一化，两个权重 1 的片段相加：髋的高度翻倍（离地 0.9 米），
   每根辅助骨离父骨的距离也翻倍（小腿扭转骨跑到脚下面，皮肤被拉成尖刺）。第一回合不会出现，所以单独测技能时一直复现不了；
   `RoeFightProbe.MatchAt` 按同一种子重放到出事的那一步，打印混合器的实际权重才看出来。现在每步都把所有片段的权重写进混合器，并在每层里归一化。

### 手臂和腿：权重、辅助骨骼、着地、迈步

看了第一版录像，手臂和脚的动作都不太对。逐项查下来是三个问题（再加上一节"踩过的坑"第 5 条的权重叠加）。

**1. 皮肤挂在辅助骨上，动捕动作驱动不到。** `RoeWeightReport` 统计每个网格的权重：

```powershell
.\tools\unity_batch.ps1 -Method RoeFighter.EditorTools.RoeWeightReport.Run     # 每个网格、每根带皮肤的骨头
```

网格本身没毛病：每个顶点最多 4 根骨，权重和都是 1，没有缺失的骨头。问题在于手臂和小腿的皮肤大部分挂在辅助骨上：

- g04 的上臂、前臂、小腿这三根人形骨一个顶点都没带。前臂的皮肤在 `ForeTwist`（408 个顶点）和 `ForeTwist1`（469 个）上，这两根骨的父骨是**上臂**；
  小腿的在 `CalfTwist` / `CalfTwist1` 上，父骨是**大腿**；上臂靠肘的一段（`LUpArmTwist1`）挂在锁骨下面的一条链上。
- a08 也是这样，只是人形骨上还带着一部分。

游戏的每个动作都逐帧给这些辅助骨写了旋转，所以在游戏里看不出问题。动捕动作只驱动人形骨，辅助骨就停在待机姿势里相对父骨的角度。
肘和膝一旦弯得和待机不一样，前臂和小腿的皮肤就跟不上：出直拳时手臂像一截折断的细棍，踢腿时靴子和小腿脱开。

做法：从游戏自己的动作里把辅助骨的规律拟合出来，放动捕动作时按这个规律驱动。

```powershell
.\tools\unity_batch.ps1 -Method RoeFighter.EditorTools.RoeHelperFit.Run       # → 人形预制体上的 RoeHelperRig 组件
```

- 在原版通用骨架上采样游戏的全部自带动作（待机、开场、三个技能、受击、倒地，约 450 帧，每秒 15 帧）。
- 对每根带皮肤的肢体辅助骨，把每根人形骨都试一遍，看它是不是"跟随的骨"：辅助骨在那根骨的坐标系里应该是固定的，最多绕骨轴扭一点。
  扭转量按"下一根骨（手、脚）的扭转 × 比例 + 上一根骨的扭转 × 比例 + 常数"做最小二乘。g04 转扇子时手腕会转过半圈、角度绕回来，
  这些离群帧剔掉后再拟合。
- 膝盖、肘部的辅助骨再试"两根相邻人形骨之间按比例插值"。
- 位置：找它保持固定偏移（1 厘米以内）的那根人形骨，没有就交给父骨。
- 战斗中每步动画算完后，`RoeHelperRig.Apply` 按动捕层的权重驱动这些骨。放游戏自己的动作时，照用游戏的数值。

拟合结果（误差是和游戏原动作的差，去掉最差的 10% 帧）：

| 辅助骨 | 跟随 | 扭转 | 误差 |
|---|---|---|---|
| `ForeTwist`（前臂靠肘） | 前臂 | 手腕扭转的 0.33 | 4–10° |
| `ForeTwist1`（前臂靠腕） | 前臂 | 手腕扭转的 0.64–0.67 | 5–11° |
| `CalfTwist` / `CalfTwist1` | 小腿 | 脚的扭转的 0.32 / 0.6 | 6–8° |
| `knee_L/R` | a08：大腿到小腿插值 0.55–0.6；g04：跟小腿 | — | 4–7° |
| `muscle_elbow_L/R` | 上臂到前臂插值 0.75–0.8 | — | 4–5° |
| `LUpArmTwist1` / `RUpArmTwist1`（g04） | 上臂 | — | 0° |
| `ThighTwist`（a08） | 大腿 | — | 4–5° |

误差偏大的有：a08 的护肩 `Scapular`（17°）和肩上两个球形护甲（14–16°），g04 的右上臂扭转骨（20°）。
这几根在游戏里还有别的驱动（护甲摆动、扇子招式），先按最接近的规律跟随。

**2. 动捕动作悬空 11–18 厘米。** 人形重定向按身高比例算髋的高度。这两个角色腿长、穿高跟，动捕演员是平底鞋，
站架时脚踝离地 0.31 米，而游戏自己的待机里只有 0.15 米。

做法：开局时从游戏的站姿里量出脚尖骨和脚踝骨离地多高，作为"鞋底"。动捕层每步取两只脚上最低的鞋底点，把整个身体竖直挪到它正好贴地。
游戏自己的动作不做这一步。

**3. 滑步。** `RoeFightProbe.FootSlide` 让两个角色在真实的战斗逻辑里前进、后退、往两边侧步各 3 秒，测"接触点"在地上滑多快
（接触点 = 每步里两个脚踝、两个脚掌中最贴近各自着地高度的那个；连续两步都是它、且离着地高度 2 厘米以内才算）：

| | 修之前 | 修之后 | 再加上第 4 条（鞋底摆正） |
|---|---|---|---|
| 前进 | 0.7–0.8 米/秒 | 约 0.3（脚掌绕脚踝转，这种走法脚尖外撇；没有净漂移） | 0.17–0.20 |
| 后退 | 0.3 | 0.07 | 0.09 |
| 侧步 | 1.5–1.7 | 0.00–0.02 | 0.01 |

- **侧步**：上半身用拳击架势，腿由代码迈步（`FighterRig.SideStep`）。着地的脚钉在落点不动；它落后于"身体下方该在的位置"超过半步时，
  就抬起来沿一道弧线（0.17 秒、抬高 7 厘米）落到前方，落点选在下一次着地的中段正好回到身体下方。两脚交替，先迈前导脚。
  腿用两节骨 IK 弯过去，膝盖保持原来的朝向。身体移动多快，步子就迈多快，脚不会滑。
- **走路**有三步：
  1. 每个角色开局时把走路、后退、跑步的动作原地放一个周期，量出着地的脚掌相对身体往后移多快（步速）。
     a08 前进 0.76、g04 0.78 米/秒，后退都是 0.56。动作按"移动速度 ÷ 步速"的倍率播放。
  2. 动捕走路里着地脚相对身体的速度在一步之内并不均匀（0.7–1.3 米/秒），所以身体不匀速移动：每步姿势算完后，
     看着地的那部分脚（脚掌或脚踝）在这一步里相对身体往后移了多少，就把身体往前挪多少。只沿走路方向挪，
     最多是设定速度的 2.5 倍。身体自然一顿一顿地走，平均速度还是设定值。
  3. 重定向到长腿骨架后，落脚那几帧脚还会贴着地往前蹭。最后一步是脚步锁定：动画判定某只脚着地时，把它的落点锁住，
     用同样的 IK 把腿弯过去；动画把脚抬起来时松开，脚离落点超过 25 厘米时也松开。第 2 步读的是锁定之前的动画姿势，两者不会互相推着走。
- 坑：测步速是在 `Init` 里放动作测的。上一局留下的脚步锁定开关如果没复位，脚被锁在地上，步速就测小了，后退一下子变成 3 倍速。

**4. 高跟鞋：站着时踮着脚尖。** 用户看了之后说 Inase 的脚还是不对。正面镜头看不清，写了 `RoeFightProbe.Feet`：在真实的战斗逻辑里
从侧面贴着地面拍一个角色的脚，并逐帧量每只脚的倾角、脚踝和脚趾骨的高度，以及蒙皮后鞋子"后半（鞋跟）"和"前半（前掌）"最低点离地多高。

```powershell
.\tools\unity_batch.ps1 -Method RoeFighter.EditorTools.RoeFightProbe.Feet -Graphics [-Extra '-roeChar','g04']   # → _work\feet\*.jpg + feet.txt
```

- 游戏自己的站姿（开场动作）是基准：脚的倾角 −71°（脚踝到脚趾骨朝下 71°），鞋跟和前掌最低点都在地面 ±2 厘米内。
- 动捕站架里，鞋跟离地 **10–11 厘米**、前掌插进地面 2–3 厘米：等于踮着脚尖站，另一只脚还常悬空 1–3 厘米。
  动捕演员穿平底鞋，脚踝角度照搬到这双高跟凉鞋上，脚就往前倾了；前掌（脚趾骨）也是平底鞋的弯法。
- 做法（`FighterRig.Tick`，只在动捕层生效，游戏自己的动作不动）：
  1. `Init` 时从站姿里记下每只脚相对"它在地面上指的方向"是怎么摆的，以及脚趾骨的局部旋转。
  2. 每步先按原样找出最低的鞋底；脚踝离自己站立高度不到 3 厘米的脚，换成站姿的摆法（方向仍然跟动作走），
     3–12 厘米之间渐变回动作自己的角度。迈步、踢腿抬起来的脚保持动作原样。脚趾骨一律用站姿的。
  3. 再把最低的鞋底放到地面上。
  4. 另一只脚如果几乎不动（每秒不到 0.35 米）、离地不到 5 厘米，就用两节骨 IK 放到地上。
- 结果：着地的脚鞋跟和前掌都贴地，误差 0.4 厘米以内（a08、g04，两个动作包都一样）；滑步也比之前小（见上表）。

### 布料：骨骼布料（照 Magica Cloth 2 的做法）

ripper_tpose 仓库里另一个窗口做了 Blender 插件 `scripts/blender_addons/bone_cloth`：照 Magica Cloth 2 的 BoneCloth 模拟骨链，烘成 MMD 动作的关键帧。
它的解算器（`core.py` 的 `simulate()`）不依赖 Blender，这里原样移植成运行时的 `RoeBoneCloth`，每步在姿势算完后跑。

- **原理**：以动画姿势为基准，每节骨头的尾端是一个质点。每个子步依次做：
  1. **惯性**：质点跟着挂点骨（裙子是骨盆，头发是头）走，只有一部分身体运动会被布"感觉"到，而且不超过限速；
  2. Verlet 积分：阻尼、限速、重力；
  3. 从根到梢逐节约束：
     - **角度恢复**：往动画方向拉回一部分，拉回的位移大部分不变成速度；
     - **角度限制**：根部严、梢部松；
     - **碰撞体**：大腿、小腿、两胯的胶囊。骨段整段当胶囊去碰，不会从两腿之间钻过去；动画本身就压进碰撞体的那部分不算；
     - **背挡**：不许往身体那边退到动画位置后面；
     - **地面**。

  骨长是刚性的。我们每步 1/60 秒，拆成 2 个子步，等于 120 Hz（插件和 Magica 是 90 Hz，参数按每 1/90 秒换算）。
- **哪些骨头**：按名字找，分四类，参数用插件的预设：
  - 裙子 `skirt`：比插件的"裙子"预设硬（角度恢复 0.35、阻尼 0.15、角度限制 5°/35°、惯性 0.3，见下面"g04 的裙子不自然"）；只有一节的裙饰不模拟；
  - 头发 `hair / braid / bangs / hari`：插件"头发"预设，碰躯干胶囊；
  - 链子和饰物 `skirt_chain / chain / chain_arm / dec_wrist`：更松的一组参数；
  - 胸 `breast / chest_L/R`：更有弹性、角度很小。

  有两个以上同类子骨的骨头本身不模拟，它下面的链挂在它上面。a08 是 9 组 83 根，g04 是 5 组 64 根。扇子、武器、脸上的骨头不碰。
- **碰撞体**：大腿、小腿的胶囊从网格量粗细。取"头部落在这一段上的所有骨头"（包括扭转辅助骨）带的顶点，到骨段距离的 70% 分位数：
  a08 / g04 大腿约 8.6 → 6.4 厘米，小腿约 5.4 → 3.9 厘米。
- **什么时候生效**：动捕的基础动作时全开（`FighterRig.clothWeight`，默认 1）；游戏自己的技能、受击里，裙子和头发是游戏手调的关键帧，保持原样。
  没有动画曲线的骨头（a08 的裙链、剑链）每步动画求值前先回到静止角度，否则会把上一步的模拟结果当成动画基准。
- 演示：`RoeClothDemo` 让每个角色用真实的战斗逻辑把同一段动作做两遍（站架、前进、后退、两边侧步、刺拳、直拳、踢、挥砍），
  一遍关布料、一遍开，`tools\cloth_demo_video.py` 左右拼成 `out\bone_cloth_demo.mp4`。踢腿时，关布料的腿直接穿过裙片；开布料的裙片被腿顶起来。
- 坑：同一个 Unity 会话里把场景重新打开，URP 的后期处理会丢掉 Volume 组件（断言 `SetupColorLut colorAdjustments cannot be null`），
  从第二次开始每帧都是黑的。演示工具只开一次场景，每组之间重新 `Setup`。
- **裙子的基础姿势（10-02 晚）**：用户说 Inase 的裙子还是不对。`RoeFightProbe.Skirt` 从前、左、后三面拍胯和腿：游戏自己的动作里
  两片长裙摆从胯两侧垂到脚踝；动捕动作里却像两块硬板斜着向外、向后翘出约 45°，关掉布料也一样，所以问题在布料模拟之前。
  裙子根骨挂在骨盆上，关键帧来自第 0 层的游戏待机动作，是相对骨盆的角度；`RoeFightProbe.PelvisCheck` 量出待机动作里骨盆明显侧倾
  （右胯比左胯高约 23°），动捕的骨盆是正的，裙子就跟着骨盆转偏了同样的角度。
  做法：`Init` 时从站姿里记下骨盆上每片裙子的根骨（下面挂着链的子骨；a08：`Skirt_L_00`、`Skirt_R_00`、`Skirt_M_00/01`；
  g04：`Skirt_Back_01`、`Skirt_front_01`、`Skirt_Left_01` 等）相对"胯部朝向"（由两条大腿的连线定）的世界旋转。
  动捕站架时它们就是这个旋转（和游戏站姿一样垂下）；骨盆偏离站架时的转动（走路摆胯、踢腿），它们跟着转。
  站架的骨盆朝向在 `Init` 里把站架片段放一圈取平均。
- **g04 的裙子不自然（10-02 晚）**：用户接着说 Luffee 的裙子不自然。查出三件事：
  1. 上一条第一版让裙子根骨完全不跟骨盆倾斜。`RoeHelperFit.SkirtReport` 在游戏全部自带动作里比较几种跟随方式（单位：度，取误差最小的 90% 帧）：
     g04 腰间的两块小饰片几乎刚性跟骨盆（3–10），按"只跟胯部朝向下垂"算是 22；后片、装饰条也是跟骨盆最准（11–16 对 24）。
     所以改成上面说的"站架时校准、之后跟骨盆转"；没有下级骨的小饰片完全不动，刚性跟骨盆。
  2. 布料参数（插件的裙子预设：角度恢复 0.2、阻尼 0.05、角度限制 25°/60°、惯性 0.4）对 g04 太软：她每片裙子从腰上第一节就开始模拟
     （a08 的根骨下分成几条链，根骨本身钉在腰上）。`RoeFightProbe.SkirtSwing` 量每条裙链末端偏离动画姿势多少、一帧最多转多少：
     g04 站着不动平均偏 12°、一帧最多跳 14°（动画本身一帧只变 4°），走路、出招时平均 16–22°、一帧最多跳 20–39°；
     腰饰小片也在模拟，一直顶在 25° 的限制上来回跳。
     现在裙子用角度恢复 0.35、阻尼 0.15、角度限制 5°/35°、惯性 0.3，一节的裙饰不模拟。
  3. 腿把裙片推开时，推开的位移几乎全变成了速度（只扣 5% 摩擦），裙片被腿弹飞；骨段的碰撞点靠近根部时，末端还会被推出穿透深度的最多 5 倍。
     现在推开的位移九成不变成速度，杠杆最多 3 倍，一次最多推 2.5 厘米。
  结果（g04，Bandai 包，平均偏差 / 一帧最大跳动）：站架 12°/14° → 4.6°/7°；前进 16°/20° → 6.5°/17°；侧步 17°/25° → 8°/9°；
  直拳 22°/39° → 8°/28°（最大的是一条细装饰线）；后腿踢 22°/32° → 9°/16°。a08 也更安静（站架 3.5° → 1.6°）。
  对比视频 `out\g04_skirt_fix.mp4`（左：修改前，右：修改后）。

```powershell
.\tools\unity_batch.ps1 -Method RoeFighter.EditorTools.RoeFightProbe.Skirt -Graphics [-Extra '-roeChar','g04']        # → _work\skirt\<包>_<布料>_<时刻>_<方向>.jpg
.\tools\unity_batch.ps1 -Method RoeFighter.EditorTools.RoeFightProbe.SkirtSwing -Graphics -Extra '-roeChar','g04','-roeMotions','bandai1'   # 摆动的数字
.\tools\unity_batch.ps1 -Method RoeFighter.EditorTools.RoeHelperFit.SkirtReport                                     # 游戏怎么动裙子根骨
```
  `SkirtSwing` 还能带 `-roeSkirt 'restore=0.3,limitRoot=8'` 临时换裙子参数、`-roeVariant nocoll / noback / nohip` 去掉碰撞体、背挡、根骨修正，用来对照。

### 动作包：可插拔的基础动作

基础动作（站架、前进、后退、跑，以及 A–D 四个攻击）做成可以整套替换的**动作包**（`MotionPack`，在 `Generated\motionpacks\`）。
每个包写明每个角色动作用哪个人形片段、每个攻击键出哪一招（击打骨骼、有效帧、攻击距离、伤害、硬直……），以及来源和授权。
游戏自己的技能、受击、倒地不属于动作包，跟着角色走。

```powershell
.\tools\unity_batch.ps1 -Method RoeFighter.EditorTools.RoeMotionPacks.BuildBandai                    # 现在的动捕动作 → bandai1
.\tools\unity_batch.ps1 -Method RoeFighter.EditorTools.RoeMotionPacks.Import -Extra '-roeSpec','my_pack.json'   # 任何人形片段（或 BVH 动捕）→ 一个包
.\tools\unity_batch.ps1 -Method RoeFighter.EditorTools.RoeMotionPacks.Import -Extra '-roeSpec','tools/motionpacks/accad_male2.json'   # 开源动捕 → accad_male2
.\tools\unity_batch.ps1 -Method RoeFighter.EditorTools.RoeFightScene.Build -Graphics -Extra '-roeMotions','my_pack'  # 用哪个包开局
```

- 场景里会放进所有已建好的包；开局用 `-roeMotions` 指定的那个（默认 `bandai1`），游戏里按 **F3** 换下一个包（比赛重新开始，计时下方显示包名）。
- **买来的 Unity 动作怎么接**：把动作包导进工程（比如放在 `Assets/ThirdParty/某包`），写一个 JSON：

  ```json
  {
    "name": "my_pack", "title": "游戏里显示的名字", "source": "Asset Store: 包名", "license": "Asset Store EULA",
    "folder": "Assets/ThirdParty/某包", "humanoid": true,
    "roles": { "guard": "Fight_Idle", "walk": "Walk_Forward", "walk_back": "Walk_Backward", "run": "Run" },
    "strikes": [
      { "button": "A", "name": "jab", "clip": "Punch_Jab" },
      { "button": "B", "name": "hook", "clip": "Punch_Hook", "damage": 60 },
      { "button": "C", "name": "straight", "clip": "Punch_Cross" },
      { "button": "D", "name": "kick", "clip": "Kick_Roundhouse", "speed": 1.2 }
    ]
  }
  ```

  导入时：
  - `humanoid` 为真时，先把那个文件夹里的 FBX 都设成 Humanoid；
  - 片段按名字找（不分大小写，`文件.fbx:片段` 可以指定文件）；
  - 走路类片段复制一份设成循环，并扣掉水平位移（身体由格斗逻辑移动）；
  - 每个攻击放到 a08 身上采样，量出是哪只手或脚、最远够多远、多高、哪几帧在最远距离的 85% 以内（和动捕的招式表同一个算法）；
  - 没写的伤害、硬直按键位给默认值（A 轻、D 重）。

  步速、着地、脚步锁定、辅助骨、布料都是按每个角色身上的实际姿势实时算的，换包不用改别的。
  包里还可以写 `"walkSpeed"`、`"backSpeed"`（米/秒）：这套动作原本走多快就让格斗按多快走，不写就用格斗默认的 1.15 / 0.95。
- 重新导入同名的包时原地更新那个资源，场景里引用的还是它，不用重建场景。

#### 第二个包：开源动捕 `accad_male2`

先调研了十来个公开的动作数据，按"能不能直接下载、授权、有没有格斗动作"挑：

| 来源 | 授权 | 格斗动作 | 结论 |
|---|---|---|---|
| **ACCAD Male 2**（俄亥俄州立大学） | CC BY 3.0，署名即可 | 格斗架势、拳击步法、各种拳、二十来种踢、防御、闪避、挑衅、胜利；每段一个动作 | **用它**：不用登录，一个 7 MB 的压缩包 |
| Quaternius 通用动画库 1、2（免费版） | CC0 | 刺拳、直拳、勾拳、受击、倒地，**没有踢** | 留作备选（FBX，直接走 Humanoid 导入） |
| CMU 动捕库 | 几乎不限用途（不许直接转卖数据） | 空手道（135 号）、拳击、出拳踢腿、起身 | 一段十几到几十秒、120 fps，要自己一招招切；以后补招可以用 |
| 100STYLE | CC BY 4.0 | 只有走跑（各种风格，含面朝前横移） | 不够 |
| LAFAN1（育碧） | CC BY-NC-ND 4.0 | 有对打的长镜头 | 授权不许发布改编后的动作，不用 |
| Bandai Namco 数据集 2 | CC BY-NC 4.0 | 只有走、跑、挥手 | 不够 |
| Mixamo、Asset Store 免费包、Rokoko | 各自条款 | 很多 | 要登录（你的账号），留给你 |

```powershell
python tools\fetch_mocap.py --source accad      # 下载并解压到 _work\mocap\accad\male2（149 段 BVH，30 fps，厘米）
python tools\bvh_preview.py _work\mocap\accad\male2\Male2_E2_JabRight.bvh --every 4 --out sheet.png   # 火柴人连环图，挑帧范围
.\tools\unity_batch.ps1 -Method RoeFighter.EditorTools.RoeMotionPacks.Import -Extra '-roeSpec','tools/motionpacks/accad_male2.json'
```

这位演员习惯右脚在前（southpaw），站架、步法、防御都是这个架势；每一招左右两边各录了一遍，包里统一用右脚在前的版本：

| 用途 | 段落 | 说明 |
|---|---|---|
| 站架 | `E8_Bounce` | 原地轻跳的拳击架势，切出 0.8 秒一个循环 |
| 前进 / 后退 | `E3_Advance` / `E5_Retreat` | 拳击小碎步（前脚迈、后脚跟），各切出 1 秒一个循环 |
| A 刺拳 | `E2_JabRight` | 前手（右拳） |
| B 前腿回旋踢 | `G11_RoundhouseLeadingRight` | 轻脚 |
| C 直拳 | `E3_CrossLeft` | 后手（左拳），身体前压 |
| D 后腿回旋踢 | `G8_RoundhouseLeft` | 重脚，高踢 |

在 a08 身上量出来：刺拳够到 0.77 米、前腿回旋踢 1.23 米、直拳 0.78 米、后腿回旋踢 1.18 米；播放速度 1.6 / 1.5 / 1.4 / 1.4 倍。

JSON 里的 `bvh` 一块说明怎么把动捕切成片段（节选，`//` 后面是这里加的说明，真正的 JSON 里不能写；完整的见 `tools/motionpacks/accad_male2.json`）：

```json
"bvh": {
  "folder": "_work/mocap/accad/male2",
  "reference": "Male2_A1_Stand.bvh", "restFrame": 0,     // 用哪一帧自然站立建人形骨架（-1 = BVH 的零姿势本身就是 T 姿势时）
  "scale": 0.01,                                          // 米/单位，不写就按腿长判断（厘米、米、英寸）
  "segments": [
    { "name": "jab", "file": "Male2_E2_JabRight.bvh", "from": 14, "to": 50, "limb": "RightHand", "recoverSpeed": 1.5 },
    { "name": "cross", "file": "Male2_E3_CrossLeft.bvh", "from": 10, "to": 58, "limb": "LeftHand", "recoverSpeed": 2, "face": "stance:jab" },
    { "name": "guard", "file": "Male2_E8_Bounce.bvh", "loop": true, "minCycle": 0.4, "maxCycle": 1.4, "face": "stance:jab" },
    { "name": "walk", "file": "Male2_E3_Advance.bvh", "from": 10, "to": 165, "loop": true, "minCycle": 0.8, "maxCycle": 1.4, "face": "travel" }
  ]
}
```

每个段落转成一个人形片段（放在 `Generated\motionpacks\<包名>\bvh\`），后面就和任何人形片段一样：`roles` 和 `strikes` 按段落名引用。
导入器（`RoeMocap`）为此改成了通用的：

- **骨骼名自动识别**：先拆出左右（`Left`/`L`/`_L`/`l` 前缀）和名字主体，按名字找手（`wrist`/`hand`）和脚（`foot`/`ankle`），
  它上面一节是前臂/小腿，再上面是上臂/大腿，再上面同侧的是肩；髋是两条大腿和头的公共祖先；髋到头之间按两臂分叉的位置分成脊柱和脖子，
  和父骨重合（零偏移）的关节跳过。Bandai 的 `Hand_L`、CMU/LAFAN1/Mixamo 的 `LeftHand`、3ds Max 的 `Bip01 L Hand`、ASF 的 `lwrist` 都能认；
  认不出来时可以在 `bones` 里直接写。`tools\bvh_preview.py` 用同一套规则。
- 改完后重跑 Bandai 的转换，生成的片段和招式表**逐字节相同**。
- **对齐方式**（`face`）：
  - 原来的做法是出招按"伸得最远的方向"对准对手，走路按骨盆朝向。格斗架势是侧身站的（骨盆偏开对手约 55°），按骨盆对齐会斜着走，
    所以加了 `travel`（按移动方向）和 `back`（后退）。
  - 每招各按出手方向对齐时，几招的起手姿势互相差到 20° 以上（直拳打出去时身体是拧着的），从站架切过去身体会一扭。
    `stance:jab` 让起手时骨盆和刺拳起手时一样对着对手，出手过程中再平滑转到出手方向，到最远点正好对准，收回时再转回来
    （直拳转 −22°，后腿回旋踢 +14°，前腿回旋踢 −3°）。
- **收招加速**（`recoverSpeed`）：演员收拳很慢，直拳打出去 0.9 秒后才回到架势。最远点之后的部分按这个倍数加速（刺拳 1.5，其余 2）。
- **循环段落**的长度改用秒（`minCycle`、`maxCycle`），`fps` 可以降采样（CMU 那种 120 fps 的数据）。

实测（`RoeFightProbe.FootSlide -roeMotions accad_male2`，着地的接触点每秒滑多少；已含"手臂和腿"一节第 4 条的鞋底摆正）：

| | a08 | g04 |
|---|---|---|
| 前进（0.9 米/秒，动作放 1.92 / 1.40 倍） | 0.20 | 0.14 |
| 后退（0.8 米/秒，1.68 / 1.67 倍） | 0.12 | 0.10 |
| 侧步 | 0.01–0.05 | 0.02–0.03 |

小碎步本来就是后脚拖着地跟上去，所以后退比 Bandai 的正常走路（0.09）滑得多一点；没有净漂移。

演示：`RoeClothDemo.Packs` 让两个角色用同一套按键脚本（站架、前进、后退、两边侧步、A、C、D、B）把每个包各做一遍，
`python tools\cloth_demo_video.py _work\pack_demo out\motion_packs_demo.mp4 --variants bandai1,accad_male2` 左右拼起来，
每格下面写着当前按的键和那个包里这个键是什么招。`RoeFightScene.Record -roeMotions accad_male2` 用新包录一整场电脑对电脑。

坑：

1. **裙子像翅膀一样横着飘。** 第一次导入后，两个角色的裙子在新包的所有动作里都水平伸出去，关掉布料也一样。
   裙子挂在骨盆上，`RoeFightProbe.PelvisCheck` 量出骨盆骨被往前翻了 90°（腿和脊柱由各自的肌肉值摆对了，看不出来）。
   原因在建人形骨架那一步：原来用"髋 → 第一节脊柱"当"上"把骨盆摆正，ACCAD 的第一节脊柱 `ToSpine` 却在髋正后方 8 厘米、和髋一样高，
   摆正后骨盆仰面躺倒了 90°，放到角色身上就是前翻 90°。现在第一节脊柱和"髋 → 脖子"的方向差超过 30° 时改用后者（Bandai 两者只差 1.4°，结果不变）。
2. 后退第一次切到了演员停下来站着的那几帧：站着不动也"首尾很像"。现在 `travel` / `back` 的循环要求每秒至少移动 0.15 米。

## 已知问题和待定的事

- 布料只在基础动作里生效；游戏自己的技能里裙子和头发是手调的关键帧。裙子的参数按 g04 和 a08 量过调过（见"布料"一节）；
  头发、链子、胸还是 Blender 插件的预设。g04 的一条细装饰线在出招时偶尔一帧转 20–30°。
- **裙子仍然不自然（用户 10-02 晚看过 `out\g04_skirt_fix.mp4` 后："不行，还是有问题"）。** 下一步先调研 Magica Cloth 2 本身是怎么实现的，
  再决定怎么改，不再凭感觉调参数。现在的 `RoeBoneCloth` 是 Blender 插件的移植，每条骨链各自独立；
  据我所知 Magica 的 BoneCloth 能把相邻的链横向连成网（连接方式 Line / Automatic Mesh / Sequential Mesh），用距离和弯曲约束把一片裙子
  当成一块布来算，我们没有这一层——调研时先核实这一点。
- 动作包现在有 `bandai1`（默认）和 `accad_male2` 两个，买来的 Unity 动作照"动作包"一节的 JSON 接进来。`accad_male2` 是右脚在前的拳击架势，
  g04 举着大扇子打拳击架势时扇子挡在身前；只有四个攻击键，ACCAD 里还有勾拳、上勾拳、侧踢、前踢、防御、闪避、胜利动作没用上，CMU 的空手道也可以补进来。
- 一个角色播另一个角色的技能时，辅助骨用的还是原角色动作里的数值；`RoeHelperRig` 也可以用在这里，还没接。
- 布料物理：游戏用的是 Magica Cloth 2，每套服装的设置（哪些骨骼、重力、阻尼、身体碰撞体）都能取出来。买了这个插件就能直接照搬；不买就自己写弹簧骨骼。
- 武器只在游戏自己的动作里出现（放技能、开场、胜利）：a08 的剑挂在骨盆上，基础动作时会跟着拳脚乱甩；g04 的大扇子按用户的要求
  （10-02："小招的时候就不显示扇子了，大招再用扇子"）在站架、走路、普通拳脚时也藏起来，空手打。
- 五个角色着色器照反汇编改过之后（10-02），影棚里的头发比以前亮一些（游戏的头发环境光里带高光项，影棚的环境光又比游戏场景亮）；
  在游戏自己的场景光照下差别不大。对比图在 `_work\shader_compare\`。
- 眼睛的视差按游戏的数值相当强：侧着看时瞳孔会往镜头一侧偏出虹膜半径的三分之一左右，这和真实眼球（虹膜在角膜下约 3 毫米）差不多。
- s01 这个场景的空地只有 7 米宽、40 米长的一条走廊，做能绕圈的 3D 格斗偏窄；已经另挑了开阔的场景（见"开阔的战斗场景"一节）。
- 镜像的角色（屏幕左边的 a08）连带特效一起镜像了，左右手相关的特效方向和游戏里相反；技能本身没有左右之分，暂时看不出问题。
- 命中特效的点光按顶点照到地面上，地面网格稀的地方还会有一块块亮斑；游戏里同样是按顶点算的，先保持一致。
- 场景导入时还没有改写官方包脚本的引用，所以场景里游戏自己的后期（调色 LUT、景深）和灯光附加数据被当成缺失脚本去掉了；`import_skills.py` 的做法搬过去就能用上。
- 战斗动作里脚尖会短暂穿到地面以下 10–20 厘米，这是原动作就有的。

## 环境

- Unity 6000.4.12f1，装在 `E:\tools\Unity\Hub\Editor\6000.4.12f1`；Unity Hub 3.22（winget 装的）。选 6000.4 是因为 UFE 2.7.3 在 Asset Store 上要求不低于 6000.4.1。
- 这台电脑直连 Unity 的下载服务器会被转到中国站（国际版安装包 404），只能走环境变量里的代理，而代理单连接只有约 30 KB/s。`tools\fetch_parallel.py` 用 64 个连接分段下载（约 1.8 MB/s），`fetch_fill.py` 补最后几个慢块。
- Git Bash 里的 curl 访问 127.0.0.1 也会走代理而失败，本机服务用 Python（`ar.py` 里关掉了代理）或 PowerShell 访问。
