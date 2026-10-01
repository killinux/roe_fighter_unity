# ROE Fighter（Unity 版）

用 Rise of Eros（ROE）的角色做的 3D 格斗游戏。引擎是 Unity 6000.4.12f1 + URP，对战框架准备用 UFE 2（Universal Fighting Engine 2，还没到货）。
首发两个角色：a08（Inase）和 g04（Luf）。

从游戏里取出的素材和买来的插件都不进这个仓库（见 `.gitignore`）：`Assets/ROE`、`Assets/RoeFighter/Generated`、`Assets/UFE*`、`_work`、`out`。
这些素材来自商业游戏，只在本机自己用。

## 现状（2026-10-01）

| 内容 | 状态 |
|---|---|
| a08、g04 的高模、游戏材质、动作、音效进 Unity | 完成 |
| 五个角色着色器（服装、皮肤、头发、眼睛、眉毛睫毛） | 完成，按游戏的参数表重写，还在对着游戏画面微调 |
| 展示视频（两人各播一遍自带的待机、三个技能、受击，带技能音效） | `out\a08_g04_showcase.mp4` |
| Unity 人形骨架 + 自带动作转成人形动作 | 完成，和原动作的平均误差 0.03–1.1 厘米 |
| 互相套用动作（g04 播 a08 的技能等） | 能播；头发裙子没有物理，辅助骨骼还没有驱动 |
| 游戏自己的战斗场景（s01，城堡屋顶） | 完成，烘焙光照原样可用；自动找一条没有遮挡的对战线 |
| 技能表：每个技能的命中时刻、伤害分段、冲刺位移、震屏、特效、音效、语音 | 完成，从游戏的时间轴和技能演出设置里读出来 |
| 场景里的对打演示（按技能表演，带特效、音效、语音，最后一招击倒） | `out\a08_g04_duel_s01.mp4` |
| 技能特效（游戏原本的粒子预制体 + 六个重写的特效着色器 + 游戏原本的时间轴） | 完成：动作和特效都由游戏自己的时间轴驱动，打在对手身上的命中特效按技能表生成 |
| UFE 对战 | 等 UFE 2 到货 |

## 目录

```
Assets/RoeFighter/Shaders/     角色、场景、特效着色器和公共代码
Assets/RoeFighter/Runtime/     运行时也要用的代码（技能表的数据结构）
Assets/RoeFighter/Editor/      编辑器脚本：建工程设置、拼角色、拍图、转动作、场景、对打演示、各种检查
Assets/RoeFighter/Settings/    渲染管线、影棚的后期和地面
Assets/ROE/                    （不入库）从游戏取出的素材，按角色和游戏包分文件夹
Assets/RoeFighter/Generated/   （不入库）拼好的角色预制体、人形骨架、转好的动作
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

AssetRipper 还原不出游戏着色器的源码，只给参数表（名字、范围、开关）。五个角色着色器是按参数表重写的，**参数名和游戏的一样**，所以游戏的材质文件原样能用，`import_ripped.py` 只把材质里的着色器引用换成这里的。

后来发现源码虽然没有，**编译后的程序是有的**：`tools\shader_asm.py` 从 `heros_shader_collection.ab` 里取出每个着色器、每种开关组合的 DirectX 11 程序，用 Windows 自带的 `d3dcompiler_47.dll` 反汇编，并把常量缓冲区的布局（哪个偏移是哪个材质参数）、贴图槽位、混合和深度状态一起写成文本（`_work\shader_asm\`）。照着它就能把算法一行一行对出来。特效着色器（见"技能"一节）就是这样重写的；五个角色着色器还没有照它核对，这是下一步调画面的依据。

```powershell
python tools\shader_asm.py                                   # 列出游戏的全部着色器
python tools\shader_asm.py "Pinkcore/Particles"              # 名字里含这段的都导出
python tools\asm_variant.py _work\shader_asm\Pinkcore_Particles_UnlitMaster.txt f "DISSOLVE DISTORTION _COLORMODE_MULTIPLY _EMISSIONMODE_SELF"
```

| 着色器 | 对应游戏的 | 要点 |
|---|---|---|
| `ROE/Character` | ErosLit/Character | MGA 贴图（R 金属、G 光滑、B 遮蔽）、法线 + 细节法线、自发光、抖动透明、边缘光 |
| `ROE/Skin` | Skin | 游戏自带的皮肤 LUT（横轴光照、纵轴曲率，曲率在 MGAC 的 A 通道）、漫反射用模糊过的法线、毛孔细节法线、薄处透光 |
| `ROE/Hair` | KajiyaKayHair | 灰度发丝贴图乘发色、发丝遮蔽、两层高光；按不透明裁切画（配合 MSAA 抗锯齿），能投影、能参与景深 |
| `ROE/Eye` | Eye | 在眼球 UV 圆盘里合成：眼白、虹膜、放大中心得到瞳孔、角膜缘暗环、视差、高光 |
| `ROE/Eyebrow` | Eyebrow | 半透明叠在脸上 |

光照的算法是自己写的，不是游戏原来的。几个和游戏不一定一致、可以调的参数（材质面板里标了 ours 或在 Ours 分组）：皮肤的 `_CurvatureScale`、`_TranslucentScale`，头发的 `_SpecularScale`、`_DiffuseWrap`，眼睛的 `_PupilBoost`、`_LimbusStrength`。

踩过的坑：
- 眼球网格的切线不可靠，用它算视差会让整只眼睛变黑。眼睛着色器改用屏幕空间导数自己算切线。
- 虹膜贴图里的瞳孔只有虹膜的 12%，靠放大贴图中心得到正常大小。
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

## 已知问题和待定的事

- 套用别人的动作时：头发、裙子、胸不会动（要接布料物理）；扭转辅助骨骼没有驱动（要写一个按肢体旋转比例跟随的小脚本）。这两件等 UFE 的基础动作到手再做，才有东西可对照。
- 布料物理：游戏用的是 Magica Cloth 2，每套服装的设置（哪些骨骼、重力、阻尼、身体碰撞体）都能取出来。买了这个插件就能直接照搬；不买就自己写弹簧骨骼。
- a08 的剑在基础动作里怎么处理还没定：现在是跟着骨盆走。可选的做法是基础动作时隐藏、放技能时出现。
- 头发比游戏里的略亮，眉毛偏淡。现在能读到游戏着色器的反汇编了，五个角色着色器要照它核对一遍，而不是对着视频猜。
- s01 这个场景的空地只有 7 米宽、40 米长的一条走廊，回合制够用，做能绕圈的 3D 格斗偏窄。游戏有 194 个战斗场景，要挑开阔的。
- 镜像的角色（屏幕左边的 a08）连带特效一起镜像了，左右手相关的特效方向和游戏里相反；技能本身没有左右之分，暂时看不出问题。
- 命中特效的点光按顶点照到地面上，地面网格稀的地方还会有一块块亮斑；游戏里同样是按顶点算的，先保持一致。
- 场景导入时还没有改写官方包脚本的引用，所以场景里游戏自己的后期（调色 LUT、景深）和灯光附加数据被当成缺失脚本去掉了；`import_skills.py` 的做法搬过去就能用上。
- 战斗动作里脚尖会短暂穿到地面以下 10–20 厘米，这是原动作就有的。

## 环境

- Unity 6000.4.12f1，装在 `E:\tools\Unity\Hub\Editor\6000.4.12f1`；Unity Hub 3.22（winget 装的）。选 6000.4 是因为 UFE 2.7.3 在 Asset Store 上要求不低于 6000.4.1。
- 这台电脑直连 Unity 的下载服务器会被转到中国站（国际版安装包 404），只能走环境变量里的代理，而代理单连接只有约 30 KB/s。`tools\fetch_parallel.py` 用 64 个连接分段下载（约 1.8 MB/s），`fetch_fill.py` 补最后几个慢块。
- Git Bash 里的 curl 访问 127.0.0.1 也会走代理而失败，本机服务用 Python（`ar.py` 里关掉了代理）或 PowerShell 访问。
