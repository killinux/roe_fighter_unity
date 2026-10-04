# ROE Fighter（Unity 版）

用 Rise of Eros（ROE）的角色做的 3D 格斗游戏。引擎是 Unity 6000.4.12f1 + URP。原计划用 UFE 2（Universal Fighting Engine 2）做对战框架，暂时买不了，所以先自己写了一套能玩的格斗逻辑（见"格斗"一节）；角色、动作、技能、特效这些数据以后换 UFE 也照样能用。
角色：a08（Inase）、g04（Luf）首发，10-03 晚加了 b10（Kart）和 g05（Luf 的女神套装），10-04 加了《死或生 6》（DOA6）的霞（Kasumi）；开局先进选人界面。

从游戏里取出的素材和买来的插件都不进这个仓库（见 `.gitignore`）：`Assets/ROE`、`Assets/DOA`、`Assets/RoeFighter/Generated`、`Assets/UFE*`、`_work`、`out`。
这些素材来自商业游戏（ROE、DOA6、DOA5LR），只在本机自己用。

## 现状（2026-10-04）

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
| 布料：裙子、头发、链子、胸的骨骼布料（移植自 bone_cloth 插件，照 Magica Cloth 2） | 完成，基础动作时生效；对比视频 `out\bone_cloth_demo.mp4`；10-02 晚裙子按游戏站姿垂下（之前翘出 45°），对比图 `out\skirt_fix_1002.jpg`。调研 Magica Cloth 2（`docs/magica-cloth-2.md`）后照它的做法重做：裙子跟腿走、一片布连成网，F4 切换新版 / 旧版 / 关，以后可接 Magica；对比视频 `out\skirt_magica_style.mp4`；之后又修了屁股附近后片翘起（裙子的基准改成游戏站姿），对比视频 `out\skirt_rest_compare.mp4`；10-03 裙子改成自然下垂、贴着身体（F5 可切回），对比视频 `out\skirt_drape_compare.mp4` |
| 动作包：基础动作可以整套替换（F3 切换，买来的人形动作写个 JSON 就能导入） | 两个包：`bandai1`（默认）和开源动捕做的 `accad_male2`（拳击架势、步法、前后腿回旋踢，10-02 晚）；对比视频 `out\motion_packs_demo.mp4`，用新包的整场对打 `out\fight_cpu_match_accad.mp4` |
| 能玩的格斗：移动、侧步、四个攻击键、防御、受击、倒地、三个技能（含超必杀）、能量槽、回合、计时、HUD、电脑对手、场地自带的战斗音乐 | 能玩：`out\ROEFighter\ROEFighter.exe`（10-02 打包），或在编辑器里打开场景按 Play；电脑对电脑的录像 `out\fight_cpu_match_1002b.mp4` |
| g04 的普通攻击换成不知火舞（DOA6）的四招；扇子缩到 0.7 倍 | 完成（10-03），见"不知火舞的普通攻击"一节；对比视频 `out\g04_mai_strikes.mp4`，扇子 `out\weapon_size\g04_fan_sizes.png` |
| UFE 对战 | 暂时买不了；先用自己的格斗逻辑 |
| b10（Kart）、g05（Luf 女神）加入战斗：模型、动作、技能和特效、音效，都能爆衣到全身 | 完成（10-03 晚），见"新角色：b10 和 g05"一节；爆衣演示 `out\clothes_burst_demo_b10_g05.mp4`，电脑对电脑整场 `out\fight_cpu_match_b10_g05.mp4`，检查图 `out\clothes_burst\unity\b10_sheet.png`、`g05_sheet.png`。之后（10-03 晚第二轮）：g05 受击、倒地时落到地上，放技能时慢慢升空，飘带在游戏动作里也交给布料，对比 `out\g05_hover_fix.mp4`；a08、g04 的动作按新的肢体重算重转了。10-04 第三轮：g05 的脚和游戏对齐（后脚不再朝后站），对比图 `out\g05_feet_1004.jpg` |
| 选人界面：四个角色的头像卡片，选中的两人站在场上；两边选同一个角色时复制一份 | 完成（10-03 晚），见"选人界面"一节；演示 `out\select_screen_demo.mp4` |
| Luffee（g04、g05）的站架也换成不知火舞的（DOA6 00000） | 完成（10-03 晚），对比视频 `out\luffee_mai_stance.mp4`（左：动作包的拳击架势；右：不知火舞的架势和四招） |
| 爆衣（比赛中把衣服打掉） | 完成到第二步（10-03）：超必杀的最后一下打中、被 KO，各掉一段（每人四段），决胜的 KO 把剩下的全打掉，整场比赛不回来，F6 开关；衣服下面换成家族的裸体底模，所以能掉到全身（g04 留着鞋），见"爆衣"一节；演示 `out\clothes_burst_demo.mp4`，检查图 `out\clothes_burst\unity\a08_sheet.png`、`g04_sheet.png`，电脑对电脑整场 `out\fight_cpu_match_burst.mp4`。调研和后面几步（补身体、打哪破哪）见 [`docs/clothes-burst.md`](docs/clothes-burst.md) |
| DOA6 的霞加入战斗；调研 DOA6、DOA5LR 的胸、头发、衣服物理；物理做成可插拔（F4 换方案，以后可接 Magica Cloth 2） | 完成（10-04），见"霞（DOA6）和可插拔物理"一节和 [`docs/doa-physics.md`](docs/doa-physics.md)；对比视频 `out\kas_physics_body.mp4`、`out\kas_physics_chest.mp4`（左：骨骼布料，中：DOA6 的物理，右：关），电脑对电脑 `out\kas_fight_test.mp4` |

## 目录

```
Assets/RoeFighter/Shaders/     角色、场景、特效着色器和公共代码
Assets/RoeFighter/Runtime/     运行时也要用的代码：技能表的数据结构，Fight/ 下是格斗逻辑
Assets/RoeFighter/Editor/      编辑器脚本：建工程设置、拼角色、拍图、转动作、场景、对打演示、各种检查
Assets/RoeFighter/Settings/    渲染管线、影棚的后期和地面
Assets/ROE/                    （不入库）从游戏取出的素材，按角色和游戏包分文件夹
Assets/DOA/                    （不入库）DOA6 角色的模型、贴图、物理数据、动作（Assets/DOA/<id>）
Assets/RoeFighter/Generated/   （不入库）拼好的角色预制体、人形骨架、转好的动作、mocap/ 下是动作捕捉转成的基础动作
Assets/RoeFighter/Scenes/      （不入库）生成的对战场景（游戏的战斗场景 + 两个角色 + 格斗逻辑）
tools/                         Python / PowerShell 工具
docs/                          调研记录（Magica Cloth 2、爆衣、DOA 的物理是怎么做的）
_work/                         （不入库）游戏包的副本、提取结果、日志、渲染帧
out/                           （不入库）给人看的视频和图
```

## 从零重做一遍

```powershell
cd E:\code\othercode\roe_fighter_unity

# 1. 把角色用到的游戏包拷到 _work\bundles（a08、g04 两个是 94 个、约 430 MB）
python tools\stage_bundles.py _work\bundles a08 g04 b10 g05

# 2. 另开一个窗口启动 AssetRipper 并留着它，然后回到这个窗口导出成 Unity 工程
#    （另一个窗口）E:\tools\AssetRipper_1.3.14\AssetRipper.GUI.Free.exe --headless --port 5599 --log-path _work\assetripper.log
python tools\rip.py          # 设置 → 读入（缺的依赖包自动补）→ 导出到 _work\ripped，约半分钟

# 3. 拷进工程，材质改指向这里的着色器，写出清单
python tools\import_ripped.py

# 3b. 爆衣用的家族裸体底模（a08 用 a01，g04 用 g01，b10 用 b01，g05 用 g01_fm）：单独还原，只挑身体、材质、贴图，不动角色清单（见"爆衣"一节）
#     （--log 必须是 AssetRipper 服务启动时给的那个日志文件，rip.py 靠它找缺的依赖包）
python tools\stage_bundles.py _work\bundles_nude --nude a01 g01 b01 g01_fm
python tools\rip.py --bundles _work\bundles_nude --out _work\ripped_nude --log _work\assetripper.log
python tools\import_nude.py a01 g01 b01 g01_fm

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

双击 `out\ROEFighter\ROEFighter.exe` 就能玩（窗口 1600×900，可以拉大；整个文件夹 703 MB，四个角色以后 1.1 GB，打包 1.5–4.5 分钟，不能单独拷 exe）。开局先进选人界面（见"选人界面"一节）。
也可以在编辑器里打开 `Assets\RoeFighter\Scenes\Fight_e23_steel_s02.unity` 按 Play。默认 1P 是人、2P 是电脑。
第一次打包失败过一次：Unity 自带的 `MovedFromExtractor` 处理 `UnityEngine.IMGUIModule.dll` 时退出码 3、没有任何输出，原样重跑就成功了。
按键：

| | 1P | 2P |
|---|---|---|
| 左右移动（朝对手走 / 后退，按住后退 = 防御） | A / D（或手柄摇杆） | ← / → |
| 侧步（往画面里 / 往画面外，绕着对手转） | W / S | ↑ / ↓ |
| A–D 四个攻击（`bandai1`：刺拳、挥砍、直拳、回旋踢；`accad_male2`：刺拳、前腿回旋踢、直拳、后腿回旋踢） | J、K、U、I（手柄 0–3） | 小键盘 1、2、4、5 |
| 换动作包（比赛重新开始） | F3 | |
| 换物理方案（各用自己游戏的 / Magica 式骨骼布料 / DOA6 的物理 / 只有胸用 DOA6 / 10-02 旧版 / 关；比赛重新开始，见 `docs/doa-physics.md` 第 4 节） | F4 | |
| 裙子：自然下垂 / 跟腿走（立即生效） | F5 | |
| 爆衣开 / 关（关掉时立刻全部穿回去；`ROEFighter.exe -roeBurst 0` 以关闭状态启动） | F6 | |
| 技能 1、技能 2、超必杀（要满能量槽） | L、O、P，或 236+J/U、214+J/U、236236+J/U | 小键盘 3、6、9 |
| 电脑接管 1P / 2P | F1 / F2 | |
| 选人界面：换卡片 / 确认 / 退回（只有 1P 是人时，先选自己的，再替电脑选） | A、D / J（或回车、空格）/ K | ←、→ / 小键盘 1 / 小键盘 2 |
| 回选人界面（比赛结束后按回车也回到这里，上一场的两人还选着，确认两次就是再来一场） | F7 | |

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
- **Magica Cloth 2 本身是怎么做的（10-02 晚调研）**：用户看了上面的对比视频说"不行，还是有问题"，于是先查清 Magica 的做法，全文在
  [`docs/magica-cloth-2.md`](docs/magica-cloth-2.md)。要点：
  - 底子和我们一样（骨骼当质点，沿基线做角度恢复、角度限制，加惯性、碰撞、背挡）。但 Magica 能把相邻骨链横向连成网，
    一片布的几条链之间有距离、弯曲约束，横边也参加碰撞；我们只有各自独立的链。
  - 查蒙皮：a08 每侧的长裙是一整片布挂在前、侧、后 3 条链上，g04 的前片挂在 2 条链上。视频里 g04 前片下摆折起、卷边，就是两条链各摆各的。
  - Magica 对裙子的头号建议是让"动画姿势"跟腿走（美术蒙皮、逐个动作调，或它的 Custom Skinning），模拟叠在上面；
    我们动捕动作里的基准是站架里的裙子钉在骨盆上，腿每一步都插进去，全靠碰撞推开。
  - 游戏自己的 95 个 Magica 组件全是大厅里的头发模板（Line、惯性 0），战斗里的裙子是手调动画，照搬游戏的设置解决不了。
  - 改法建议：裙子的动画姿势跟腿走（用游戏手调的裙子动画标定蒙皮权重）、一片布的链横向连起来、惯性分世界和局部；或者买 Magica。
  - 数据：`python tools\magica_survey.py settings | links <角色> | picture <图>`；图 `out\skirt_sheets_1002.png`。
- **照 Magica 的做法重做（10-02 晚）**：用户选了上面的方案 A，"做成可插拔的方式，先用A，后续我想办法买到 Magica 再切换"。细节和全部数字在
  [`docs/magica-cloth-2.md`](docs/magica-cloth-2.md) 第 9 节。
  - **可插拔**：布料后端接口 `IRoeCloth`（`Runtime/RoeCloth.cs`），游戏里 **F4** 依次换：Magica 式骨骼布料（默认）/ 10-02 旧版 / 关。
    10-04 扩成按类别分配解算器的"物理方案"（各用自己游戏的、DOA6 的……），见"霞（DOA6）和可插拔物理"一节。
    Magica Cloth 2 的适配器草稿 `Runtime/RoeMagicaCloth.cs` 已按手册的运行时构建接口写好（同样的分片、参数、腿部胶囊），
    装上插件后在 Player 设置里加 `ROE_MAGICA` 宏就会编译、出现在 F4 里；没装时不参与编译。它按自己的时钟在播放模式里跑，批处理检查和录视频时不动。
  - **裙子的动画姿势跟腿走**（`RoeSkirtRig`，`RoeHelperFit.FitSkirt` 拟合）：每节裙骨的尾端由胯部朝向、骨盆、两条大腿、两条小腿按权重带着走，
    权重用游戏手调的裙子动画拟合。在拟合没用到的游戏动作上，裙骨方向偏离游戏关键帧：a08 35.5° → 31.1°，g04 47.2° → 37.2°（剩下的主要是美术加的摆动，由模拟补）。
    基准和拟合时一样是游戏站姿：骨盆、大腿、小腿从站姿里动了多少，权重在它们身上的裙骨就跟着动多少（第一版的基准是动捕站架，见下面"屁股附近翘起来"）。
  - **一片布连成网**：从蒙皮自动找跨链的三角面，a08 每侧前、侧、后 3 条链连起来，后腰两条短片连起来，g04 前片两条链连起来；横边保持距离、也参加碰撞。
  - 另外：惯性分世界（角色在场上移动）和局部（动画），Tether，离动画姿势的最大距离，重置后 0.1 秒稳定，碰撞只放过站姿里本来就有的深度。
  - 结果（`SkirtSwing`，各段动作"裙子插进腿部胶囊多深"的平均，括号里是最深，厘米；胶囊按皮肤 70% 分位数量，比腿略粗，所以不是看得见的穿模深度，只用来比较）：

    | | 旧版 | 新版第一版（基准：动捕站架） | 新版（基准：游戏站姿） |
    |---|---|---|---|
    | g04，Bandai 包 | 7.5（9.1） | 2.2（5.8） | **1.9**（6.2） |
    | g04，ACCAD 包 | 6.8（9.6） | 1.8（3.4） | **1.3**（2.9） |
    | a08，Bandai 包 | 6.9（8.5） | 4.6（7.1） | **2.9**（8.2） |
    | a08，ACCAD 包 | 7.6（8.4） | 4.8（8.2） | **3.7**（6.4） |

    一帧里裙摆最大的跳动差不多（旧 15–28°，新 14–24°）；横边在踢腿时还会被拉长，现在最多 40%（第一版 35–59%）。
  - 看图：`out\skirt_compare_g04.jpg`、`out\skirt_compare_a08.jpg`（旧版 / 新版，正面、侧面、背面，前进、后退、侧步、踢、直拳）；
    视频 `out\skirt_magica_style.mp4`（46 秒，左旧右新，Bandai 包和 ACCAD 包，两个角色）。g04 回旋踢时，旧版前片竖直穿过抬起的大腿，新版搭在大腿上。
- **屁股附近翘起来（10-02 晚）**：用户问"还是裙子，接近屁股的部分翘起来了一点点，是什么刚体还是什么设置得有问题么"，"尤其是luffee"。
  - 不是刚体，也不是布料参数：F4 关掉布料一样翘，问题在模拟之前的基准姿势。
  - g04 的后片在游戏里几乎钉在骨盆上动（拟合权重：骨盆 0.92）。游戏站姿的骨盆是歪的（右胯高约 23°），后片搭在右边屁股上。
    第一版把骨盆、腿的基准放在动捕站架，站架里裙子和站姿里一模一样（相对胯部朝向）；可动捕站架的骨盆和站姿的差 25–31°（`RoeFightProbe.SkirtRests`），
    屁股跟着骨盆转了、后片没跟着转，后片上端就离开了屁股。
  - 改法：基准也用游戏站姿（`RoeSkirtRig.RestOnStance`，默认开），和拟合时一致，骨盆转多少，钉在骨盆上的后片就转多少；
    挂在胯上的裙根也改用站姿的骨盆做参照，否则裙根和从它下面指出去的骨对不上。a08"站架里裙子本来就伸进腿 3.7 厘米"其实就是这处没对齐。
  - `RoeFightProbe.ButtGap` 把皮肤烘出来，在髋关节下方 5 厘米处量后片内侧到屁股的距离（厘米，正数 = 中间有空气）：

    | g04 | 站架 | 前进 | 后退 |
    |---|---|---|---|
    | 第一版（基准：动捕站架） | 3.4 | 4.8 | 3.0 |
    | 现在（基准：游戏站姿） | **0.6** | **1.8** | **0.4** |

    基准姿势插进腿多深（`SkirtDepth`，Bandai 包，平均厘米）：a08 站架、前进、后退、跑都从约 3.5 降到 0.3–0.6；g04 前进、后退、跑多了约 1（5.3 → 6.3），模拟之后反而更浅（上表）。
  - 看图：`out\skirt_butt_g04.jpg`（右侧、右后特写，上面一行是游戏原版站姿做参照），`out\skirt_rest_a08.jpg`（a08 正面、侧面、背面，没有翘回 45°）；
    视频 `out\skirt_rest_compare.mp4`（23 秒，从右后方跟拍胯部，左旧右新，两个角色）。
- **自然下垂（10-03）**：用户看了上一条之后问"能自然下垂么，还是有一点翘"。
  - 原因：跟腿走的基准姿势是游戏站姿里的裙子被腿带着走。游戏站姿前倾、屁股往后翘，裙子顺着那个身体斜着挂，换到动捕比较直的身体上就斜着翘出去：
    没贴身体的裙骨平均偏离竖直 17–23°（游戏站姿本身也有 16–20°，`RoeFightProbe.SkirtHang`）。
  - 做法（`RoeSkirtRig.Drape`，默认开；游戏里 **F5** 在"自然下垂 / 跟腿走"之间切，屏幕上方显示 `SKIRT: hangs / fitted`）：
    1. 身体表面（`Runtime/RoeBodySurface.cs`）：开局时从蒙皮网格取胯到脚的皮肤点（胯部只取皮肤材质，腰带饰物不算；腿上连护甲、鞋都算），2.5 厘米取一个，
       只留朝外的面（护甲、鞋的内侧面会把裙子往里拉）；每一步按骨骼重新蒙皮，约 1400 个点，每个角色每步约 0.5 毫秒。
       离皮肤多远用附近多个点的切平面距离按高斯权重平均（不取"最近一个点"：最近点会在两块皮肤之间来回跳，裙子一帧跳几十度）。
    2. 每节裙骨从上一步的方向往竖直方向落（时间常数 0.05 秒；每步从头算会在腿的两侧之间跳），碰到皮肤就沿皮肤法线推开：
       骨的中线离皮肤 1.2 厘米加这节布本身的厚度（下摆的毛边有几厘米厚）。只算朝这条链那一侧的皮肤（前片不会被推进两腿中间），抬起的大腿从下面托住。
    3. 贴着身体的平布绕自己的骨转一下，平贴在皮肤上（最多 60°）：平布斜着压在圆屁股上，一边会陷进去。
    4. 每条链下半段（链长 45% 以下）再拿这节布自己的 24 个顶点去碰皮肤，最深的那个决定推多少（每轮最多 2 厘米，最多把骨抬到偏离竖直 45°）：
       g04 后片下摆的毛边是从侧面包住脚后跟的，骨的中线离腿还有 6–7 厘米。上半段不这么做：为了一个角把整片后片从屁股上推开，又翘起来了。
    5. 布料模拟：这样挂好的裙子在腿部胶囊（比腿略粗）里允许和基准姿势一样深，不然胶囊又把它推出去；裙片之间的横边不再单独碰胶囊（和横边长度打架，a08 一步抖 13°）。
  - 数字（Bandai 包，布料开）：没贴身体的裙骨平均偏离竖直（站架 / 前进 / 后退）a08 20 / 19 / 23° → 11 / 8 / 9°，g04 17 / 19 / 19° → 11 / 15 / 14°；
    裙子网格陷进皮肤的顶点 a08 0 / 0 / 3% → 0 / 0 / 0%，g04 0.3 / 0.8 / 2.0% → 0.1 / 0.9 / 0.8%；g04 后片上端贴着屁股（0.6 厘米 → 贴住）。
  - 看：`out\skirt_drape_compare.mp4`（右后方跟拍胯部、再从正面，左上一版 52f11de，右自然下垂，两个角色），`out\skirt_drape_g04.jpg`、`out\skirt_drape_a08.jpg`。

```powershell
.\tools\unity_batch.ps1 -Method RoeFighter.EditorTools.RoeHelperFit.FitSkirt                                   # 裙子跟腿走的权重 → 人形预制体上的 RoeSkirtRig（在 RoeHelperFit.Run 之后）
.\tools\unity_batch.ps1 -Method RoeFighter.EditorTools.RoeFightProbe.SkirtDepth -Graphics                      # 基准姿势插进腿多深：游戏手调 / 钉在骨盆 / 跟腿走
.\tools\unity_batch.ps1 -Method RoeFighter.EditorTools.RoeFightProbe.SkirtSwing -Graphics -Extra '-roeChar','a08','-roeCloth','legacy'   # 某个后端的数字
.\tools\unity_batch.ps1 -Method RoeFighter.EditorTools.RoeFightProbe.ButtGap -Graphics -Extra '-roeChar','g04','-roeCloths','magica_style'  # 后片离屁股多远 + 特写 → _work\butt
.\tools\unity_batch.ps1 -Method RoeFighter.EditorTools.RoeFightProbe.SkirtRests                                # 两种基准差多少（每个驱动、每节裙骨）
.\tools\unity_batch.ps1 -Method RoeFighter.EditorTools.RoeClothDemo.Rests -Graphics -Extra '-roeView','backright'   # 两种基准各录一遍 → _work\rest_demo
python tools\cloth_demo_video.py _work\rest_demo out\skirt_rest_compare.mp4 --variants rest_guard,rest_stance
.\tools\unity_batch.ps1 -Method RoeFighter.EditorTools.RoeFightProbe.SkirtHang -Extra '-roeDrapes','0,1'       # 裙骨偏离竖直多少、离皮肤多远（-roeChains 逐节）
.\tools\unity_batch.ps1 -Method RoeFighter.EditorTools.RoeFightProbe.SkirtJumps -Extra '-roeChar','g04'       # 基准姿势一步最多转多少 + 每步耗时
.\tools\unity_batch.ps1 -Method RoeFighter.EditorTools.RoeClothDemo.Rests -Graphics -Extra '-roeRests','rest_stance,drape','-roeView','front'
# 裙子的开关都能在命令行改：-roeDrape 0..1  -roeClearance 0.012  -roeTwist 0..1  -roeFacing 0.2  -roeMeshClearance 0.003；ButtGap 还会报裙子网格陷进皮肤的比例（逐节）
.\tools\unity_batch.ps1 -Method RoeFighter.EditorTools.RoeFightProbe.Skirt -Graphics -Extra '-roeChar','g04','-roePacks','bandai1','-roeCloths','legacy,magica_style'
.\tools\unity_batch.ps1 -Method RoeFighter.EditorTools.RoeClothDemo.Run -Graphics -Extra '-roeCloths','legacy,magica_style','-roeOut','_work\cloth_demo_mc2'
python tools\cloth_demo_video.py _work\cloth_demo_mc2 out\x.mp4 --variants legacy,magica_style --suffix "（Bandai 包）"
```

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

### 爆衣：衣服按件掉，掉完是全身（10-03）

调研和整个方案在 [`docs/clothes-burst.md`](docs/clothes-burst.md)。规则是用户 10-03 定的（"按建议"），下午又加了一条：

- **什么时候掉**：超必杀的最后一下打中谁，谁掉下一段；谁被 KO，谁也掉下一段。
- **决胜的 KO 全掉**：决定整场胜负的那次 KO，把输家剩下的全部打掉。这条是下午加的，用户说"让爆衣都爆掉"；开关是 `FightGame.burstAllOnFinalKO`，默认开。
- **掉了就不回来**：回合之间也不恢复，新开一场（或者按 F3 / F4 重开）才穿回去。
- **怎么掉**：护甲飞出去，布料落下来，按固定顺序掉（不看打在哪里）。烧边以后再做。
- **开关**：F6（关掉时立刻全部穿回去，屏幕上方显示 `CLOTHES BURST: on / off`）。`ROEFighter.exe -roeBurst 0` 以关闭状态启动；录像也认 `-roeBurst 0/1`。

分两步做的：

1. 上午：只掉下面本来有完整皮肤的外层（游戏把衣服盖住的皮肤删了，见 `docs/clothes-burst.md` 第 2、6 节）。
2. 下午：用户说"做个 inase 结合 nude 的模型，让爆衣都爆掉"。我把家族的裸体底模接到套装骨架上，代替套装的皮肤（下面"底模身体"），之后贴身的护甲、胸甲、上衣、手套、内裤也都能掉。

两人掉什么（规则表 `Assets\RoeFighter\Editor\Burst\a08.json`、`g04.json`）：

| | 第一段 | 第二段 | 第三段 | 第四段 | 一直留着 |
|---|---|---|---|---|---|
| a08 Inase | 长裙片（左右）、胯甲、裙链、后腰垂片、腰侧细带：9 块，11,560 个三角形 | 左肩甲和肩链、项圈、右上臂链：3 块，4,541 个 | 护手（连左小臂的链环）、腕上流苏、护胫、凉鞋：11 块，6,743 个 | 胸甲、护裆：3 块，1,546 个 | 头冠、耳饰、戒指 |
| g04 Luf | 前片、左飘带、后片和下摆毛边、裙饰和流苏：5 块，7,086 个 | 主裙（前后两大片和腰上一圈）：2,636 个 | 上衣连项圈和腰带、手套：3 块，11,760 个 | 内裤：1,104 个 | 鞋、头饰 |

g04 的鞋一直穿着：她的脚和底模的高跟角度不同（差 1–1.4 厘米），鞋里那截底模删掉了，免得穿出来。

检查图 `out\clothes_burst\unity\a08_sheet.png`、`g04_sheet.png`：
- 第一行是站姿下的原样、第一段后……第四段后，正反两面；
- 第二行是分组上色。
- 掉完四段，正反两面都没看到洞或接缝（脖子、手腕、脚踝放大看过；只看了这些角度，没有逐点量）。

演示 `out\clothes_burst_demo.mp4`：
- 每人一段，打两局：每局对手先走近放超必杀，挨打的一方剩 1 点血后由电脑接着打到 KO；
- 第二局的 KO 决定胜负，剩下的全部掉光；
- 左边近景跟着挨打的一方，右边是比赛镜头，下面写着正在发生什么。

怎么做的：

- **编辑器里拆开**（`Editor\RoeBurstBuilder.cs`）：
  - 读规则表里列出的服装渲染器，按共用的顶点位置（0.1 mm 内焊接）把三角形分成连通的块。
  - 每块按"权重最大的骨头"等条件归组。同一组、同一侧的块合成一个新的蒙皮渲染器；左 / 中 / 右按绑定姿势里骨盆和两条大腿的位置算。
  - 新渲染器的骨头、材质、包围盒、阴影设置都照抄原来的；原渲染器换成去掉这些块的网格。
  - 网格存在 `Generated\<id>\burst\`，重建时原地改写，场景里的引用不会断。
  - 建比赛场景时（`RoeFightScene.MakeFighter`）每个角色拆一遍，并挂上 `RoeClothesBurst`。没有规则表的角色不拆，什么也不变。
  - 拆完、爆之前和原来一模一样：同一个姿势、同一台相机，拆前拆后逐像素比较，最大差 2/255（后期的抖动噪点），差值超过 2/255 的像素为 0。
    这是第一步时量的；第二步换上底模身体后，皮肤本身换了贴图，正面约 5–7% 的像素不一样（主要是皮肤的细节纹理）。
- **底模身体**（`Editor\RoeNudeBody.cs`，第二步）：
  - **用的是谁**：家族的裸体底模，a08 用 a01，g04 用 g01。这是游戏 H 场景用的身体，在 `chara_bare_pc_<家族>01_nk*` 包里，只导了身体网格、材质和贴图到 `Assets\ROE\a01`、`g01`。
    底模网格里还有头、眼睛、睫毛几个子网格，只用身体那个（0 号）。
  - **整块替换**：它和套装的皮肤几乎重合，所以整块代替套装皮肤（a08 `body1` 的 0 号子网格，g04 `body` 的 1 号），没有接缝。
    g04 脖子那一圈（0 号子网格）底模盖不住，留着。
  - **骨头不能按名字绑**：底模自己的骨架比套装多几十根骨头。做法是"权重转移"，每个顶点照抄贴着它的东西的骨骼权重：
    - 1.5 厘米内有套装皮肤，就抄皮肤的；
    - 皮肤被删掉的地方，抄贴身的衣服块，比如胸甲、护胫、手套。"贴身"按几何判断：一块衣服的顶点到底模身体的中位距离不超过 2.5 厘米；裙片、链子、飘带离身体远，不算。
    - 取附近最多 8 个点按距离加权，留权重最大的 4 根骨头。
    - 这样身体动起来和它代替的皮肤、护甲完全一样。
  - **永远不掉的部件下面**：离这种部件 2.5 厘米内、又不贴着套装皮肤的三角形删掉（g04 的鞋里删了 5,759 个三角形），免得穿出来。
  - **结果**：
    - a08：19,880 个顶点，17,760 个抄套装皮肤（最近顶点距离中位 1.5 毫米），2,120 个抄护甲；
    - g04：19,751 个顶点，11,294 个抄皮肤（中位 3.9 毫米），8,457 个抄衣服——她的上衣、手套、内裤下面删掉的皮肤多，体形也和底模差得多一些。
  - **规则表里的写法**：`"nude"` 一段写底模的预制体、身体渲染器、子网格、材质，要接到的套装渲染器（`attachTo`），以及它代替的皮肤（`replace`）；
    `fill` 写从底模的哪个子网格补套装没有的部分（`missingFrom`：拿来比对的套装渲染器），b10、g05 用它补领子下面的脖子（见"新角色：b10 和 g05"）。
  - **顺手修了一个老问题：皮肤 LUT 一直缺着**。
    - 套装的皮肤和脸的材质都引用游戏的皮肤散射查找表 `pc_common_skin_rgbx_Lut`，它在 `chara_tex_bare_common_prelude` 包里，当初没导进来。着色器一直用默认的白图，人物的皮肤和脸偏白偏平。
    - 这次导底模时这张图进来了。我在 `Assets\ROE\common\chara_tex_bare_common_prelude\` 给它加了一个用原引用编号（guid `0a100c…`）的副本，套装和脸也用上了，三者肤色一致，更接近游戏。
    - `stage_bundles.py` 也补上了这个包，以后重新还原时就不缺了。
- **规则表**（每个角色一份 JSON）：
  - `outfit`：放服装的渲染器（和子网格，默认全部）。
  - `groups`：按顺序排，第一条匹配的规则说了算。每条规则有这些字段：
    - `name`，以及中文名 `title`（字幕和检查图用）；
    - `stage`：1 先掉，0 = 不掉；
    - `cloth`：布料（落下、摊平）；不写就是护甲（飞出去、翻滚）；
    - `together`：左右不分开；
    - 条件：
      - `bone`：权重最大的骨头，正则；
      - `anyBone` + `anyShare`：这些骨头加起来至少占多少权重，默认 0.1；
      - `renderer`；
      - `minTriangles` / `maxTriangles`；
      - `lowestBelow` / `lowestAbove`：这块的最低点在绑定姿势里离地多高，单位米。
  - 没有规则匹配的块一直留着。日志里 `stays on (no rule)` 那行按骨头列出这些块，写新规则时照着看。
- **比赛里掉**（`Runtime\RoeClothesBurst.cs`）：
  - **触发**：`FightGame.SpecialHit` 里超必杀的最后一下，和 `Rules` 里的 KO，都调用 `BurstClothes`。
  - **拍快照**：下一次步进时，这一段的每块用 `BakeMesh` 拍下当时的姿势（在 CPU 上蒙皮），然后关掉它的渲染器。
  - **飞出去**：快照变成一个单独的刚体：
    - 重力、空气阻力；
    - 和地面接触：按每块最凸出的约 40 个顶点，用冲量法算反弹和摩擦；
    - 出不了场地的圆圈。
  - **护甲**：沿打击方向 2–4 m/s 飞出，往外 0.8–1.6，往上 1.6–2.8，每秒转 4–10 弧度；掉的时候冒游戏的命中火花。
  - **布料**：初速小、阻力大，落得慢。碰到地面后 0.45 秒内摊平：每个顶点落到离地 0.4–2.9 厘米的"褶皱"高度，双层的布朝下那层再低 2 毫米。
  - **淡出**：2.6 秒后（布料 3.4 秒）用角色着色器自己的抖动淡出（`_IGNOpacity`，阴影一起）消失。
  - **音效**：同一步只响一次命中音效。
  - **两段同时触发**（超必杀直接打到 KO）时，第二段晚 0.3 秒。
  - **可重复**：随机数来自比赛的种子，同一场录像每次一样。
  - **可调**：所有数值都是 `RoeClothesBurst` 上的公开字段，有默认值，可以在检查器里改。
  - **耗时**（批处理里量的）：一段拍快照 0.5–5.6 毫秒；加上第一次生成火花特效，整段最多 26 毫秒。这一下本来就在超必杀最后一下的顿帧（0.12 秒）或 KO 的慢动作里，看不出卡。
- **电脑对手顺手改了一处**：
  - 两人的超必杀和技能 2 在游戏技能表里没有"冲上前"那一段，只有 2.9 米内才打得中（对手半径 1 米 + 1.9 米）。
  - 电脑以前在 4.5 米内就放，2.9–4.5 米之间放的都打空。演示第一次录的时候，在开场的 3.2 米处放超必杀，两边都打空了，爆衣只在 KO 时出现。
  - 现在只在打得到的距离放（`Fighter.SpecialReach`）；会冲上前的技能 1 不变。

给别的角色加爆衣（做法是通用的，代码里没有写死哪个角色）：

1. 照 `a08.json` / `g04.json` 写 `Assets\RoeFighter\Editor\Burst\<id>.json`；
2. 出检查图，看图和日志，改规则直到分组对：
   ```powershell
   .\tools\unity_batch.ps1 -Method RoeFighter.EditorTools.RoeBurstBuilder.Check -Graphics -Extra '-roeChars','<id>'
   python tools\burst_sheet.py --chars <id>
   ```
3. 重建比赛场景（`RoeFightScene.Build`）。

只能掉"下面有完整皮肤"的块。换别的套装要先量一遍哪里缺皮肤（调研用的脚本在 `tools\research\clothes_burst\`，见 `docs/clothes-burst.md` 第 6 节）。

```powershell
.\tools\unity_batch.ps1 -Method RoeFighter.EditorTools.RoeBurstBuilder.Check -Graphics   # 检查图 → out\clothes_burst\unity
python tools\burst_sheet.py                                                                # 拼成 <id>_sheet.png（含拆分前后的逐像素对比）
.\tools\unity_batch.ps1 -Method RoeFighter.EditorTools.RoeFightScene.Build -Graphics      # 重建场景（拆分在这一步做）
.\tools\unity_batch.ps1 -Method RoeFighter.EditorTools.RoeBurstDemo.Run -Graphics         # 演示的帧 → _work\burst_demo
python tools\burst_video.py                                                                # → out\clothes_burst_demo.mp4
```

还没做的、已知的：

- 碎片是刚体快照：布料在空中不会飘也不会变形，落地才摊平；同组同侧的链子是一整串一起飞。
- 掉了的裙片，它的骨骼布料链还在模拟（看不见，只多一点计算）。
- 超必杀那一下特效很满，近景里碎片会被特效挡住一部分；KO 那一下看得最清楚。
- g04 腰侧的两个中国结：一个属于内裤，留着；另一个绑在裙饰的骨头上，第一段就掉了。左右不对称，但不太看得出来。
- g04 的底模和她的套装体形差得比 a08 多（贴着皮肤的地方中位 3.9 毫米，a08 1.5 毫米）。穿着衣服时，个别地方可能有身体从贴身衣服里透出来，要在动作里再看。
- 皮肤 LUT 补上以后，所有角色的皮肤和脸都比以前的视频暗一些、红润一些。这是游戏本来的样子；以前的视频是缺图时录的。

### 不知火舞的普通攻击（g04 自己的四招，10-03）

用户 10-03 说："luffee的普通攻击改成刚才调研的不知火舞的普通攻击，扇子缩小一点"。

- **动作从哪来**：
  - DOA6 里不知火舞的动作，在 `RRPreview.rdb` 的 `.g1a` 里，用 ripper_tpose 的 `scripts\doa6\g2a.py` 解出来（见那边的 `docs\doa6-fighting-motions.md`）。
  - 新写的 `scripts\doa6\g2a_bvh.py` 把动作写成 BVH：
    - 关节用 Mixamo 那一套名字（Hips、Spine、LeftArm……），这里的通用导入器能认；
    - 零姿势是 G1M 骨架的静止姿势（A 字形）；
    - 根运动并进 Hips 的位移；
    - 写完按 BVH 的规矩算回去和原动作比，关节位置最大差 0.0003 厘米。

  ```powershell
  python E:\code\othercode\ripper_tpose\scripts\doa6\g2a_bvh.py --check --g1m E:\game_export\DOA6\MaiShiranui\g1m_src\MAI_COS_004.g1m `
      --out _work\mocap\doa6_mai E:\game_export\DOA6\MaiShiranui\g1a\MAI00000_MAI.g1a E:\game_export\DOA6\MaiShiranui\g1a\MAI01000_MAI.g1a ...
  ```

- **挑招**：
  - 16 段候选（编号 01000–01600 的打击动作）先导成一个候选包；
  - `RoeMotionPacks.Sheet` 让 g04 把每段做一遍，每段拍 8 帧；
  - `tools\strike_sheet.py` 拼成一张表 `out\motion_sheets\doa6_mai_candidates\sheet.png`，每行写着哪只手 / 脚打、够多远、什么时候能打中。
  - 选了：A 冲步刺拳（01000）、B 中段肘击（01110）、C 扑身冲拳（01002）、D 高踢（01040）。
  - 描述在 `tools\motionpacks\doa6_mai.json`，换招改这里。
- **只给 g04 用**：
  - 动作包加了"只含攻击"（`strikesOnly`），这种包不进 F3 的轮换。
  - `FighterRig.strikePack` 是角色自己的攻击包，`Fighter.moves` 是她的招式表：有自己的就用自己的，没有就用当前动作包的。
  - 建场景时按 `-roeOwnStrikes g04=doa6_mai`（默认）挂上；包还没建的话，从 `tools\motionpacks\<名字>.json` 导入。
  - 走路、站架、侧步还是动作包的。
- **冲步**：
  - DOA 的拳大多带一步冲步，身体往前 0.6–0.7 米。格斗逻辑出招时原本不动角色，招式一收，身体就弹回原位。
  - 现在导入时把这一步从动作里拿出来（`inPlace`），量出走了多远，写进招式（`Move.travel`）。
  - 出招时格斗逻辑带着她按动作的进度往前走，看起来和原来一样，收招不弹回。
- **演示** `out\g04_mai_strikes.mp4`（`RoeClothDemo.OwnStrikes`）：同一套按键脚本（站架、前进后退、侧步、A、C、D、B），左边是原来的普通攻击，右边是不知火舞的。
- **扇子**：
  - `FighterRig.weaponScale` 缩放"带着武器自己那些骨头的那根骨"。g04 是扇子的总控 `All_Fan_ctrl`，在扇轴上，缩了还握在手里。
  - 默认 g04 0.7 倍（`-roeWeaponScale g04=0.7`）。
  - 对比图 `out\weapon_size\g04_fan_sizes.png`：原来 / 0.7 / 0.5，用 `RoeWeaponSize.Stills` 出。
  - 扇子还是只在技能、开场、胜利时出现（用户 10-02 定的）。
- **站架也换了**（用户 10-03 晚："把不知火舞的架势也给 Luffee 当站架"）：
  - DOA6 的 00000 是她的格斗架势，切出 1 秒一个循环（首尾差 0.0004），写进 `doa6_mai.json` 的 `roles.guard`；
  - 角色自己的包里有站架时，`FighterRig.UseMotions` 用它换掉动作包的站架（防御、侧步的上半身也是它），走路、后退还是动作包的；
  - 四招和站架都按刺拳起手时的朝向对齐（`face: stance:mai_jab`），出招时再平滑转向出手方向：从站架出招、收回站架，身体不会再拧一下；
  - 这是个前倾的低架势：躯干前倾 37–41°，髋高 0.78 米（拳击架势是 4°、1.11 米）。DOA6 原始数据里就是 36–43°，转换没有放大（`RoeFightProbe.Lean` 量的）；
  - g05 也是 Luffee（女神套装），默认也用这一套（`-roeOwnStrikes g04=doa6_mai,g05=doa6_mai`）。
  - 对比视频 `out\luffee_mai_stance.mp4`：左边动作包的拳击架势和招式，右边不知火舞的架势和四招，g04、g05 各一段。

### 新角色：b10 和 g05（10-03 晚）

用户 10-03 晚："目前b10，G05也把nude补全，加入战斗"。

- **b10 = Kart 的第 10 套**（游戏里的单位 `suit_kart_10`）：红色短夹克、牛仔短裤、大腿枪套、长靴，手拿斧头，技能里用双枪。家族裸体底模 b01。
- **g05 = Luf 的第 5 套**（`suit_luf_5`）：悬空坐着的女神，头后心形光环和两枝金色月桂，白色披帛、长裙片和金饰，光脚。底模是 g01 的 `fm` 版身体（`pc_g01_fm_nk`，用 g01 的皮肤材质）。
- 两个底模都和套装皮肤完全重合（贴合误差中位 0.00 / 0.01 毫米）：套装皮肤就是从这两个身体上裁下来的。

#### 加一个角色的步骤（b10、g05 就是这样加的）

```powershell
# 1. 只拷新角色的包、还原、导入（工程里已有的公共资源保留原来的 GUID，清单里原有的角色保留）
python tools\stage_bundles.py _work\bundles_more b10 g05
python tools\rip.py --bundles _work\bundles_more --out _work\ripped_more --log _work\assetripper.log
python tools\import_ripped.py --src _work\ripped_more\ExportedProject\Assets
# 2. 家族裸体底模（爆衣用）
python tools\stage_bundles.py _work\bundles_nude_b --nude b01
python tools\rip.py --bundles _work\bundles_nude_b --out _work\ripped_nude_b --log _work\assetripper.log
python tools\import_nude.py b01 --src _work\ripped_nude_b\ExportedProject\Assets\AssetBundles --sel _work\ripped_nude_b_sel
# 3. 技能：玩法预制体（拉进约 1000 个依赖包，导入时只拷用得到的）和技能表
copy "<游戏包目录>\gameplay_prefab_suit_kart_10.ab" _work\bundles_skills_more\
copy "<游戏包目录>\gameplay_prefab_suit_luf_5.ab" _work\bundles_skills_more\
python tools\rip.py --bundles _work\bundles_skills_more --out _work\ripped_skills_more --log _work\assetripper.log
python tools\import_skills.py suit_kart_10 suit_luf_5 --src _work\ripped_skills_more\ExportedProject\Assets
python tools\skill_sheet.py suit_kart_10 b10 suit_luf_5 g05
# 4. Unity：只处理新角色（-roeChars），不动 a08、g04 已经拟合好的预制体
.\tools\unity_batch.ps1 -Method RoeFighter.EditorTools.RoeFighterBuilder.BuildAll -Extra '-roeChars','b10,g05'
.\tools\unity_batch.ps1 -Method RoeFighter.EditorTools.RoeHumanoidClips.ConvertAll -Extra '-roeChars','b10,g05'
.\tools\unity_batch.ps1 -Method RoeFighter.EditorTools.RoeHelperFit.Run -Extra '-roeChars','b10,g05'
.\tools\unity_batch.ps1 -Method RoeFighter.EditorTools.RoeHelperFit.FitSkirt -Extra '-roeChars','b10,g05'    # 没裙子就跳过
# 5. 爆衣规则表 Editor\Burst\<id>.json，看检查图改到分组对
.\tools\unity_batch.ps1 -Method RoeFighter.EditorTools.RoeBurstBuilder.Check -Graphics -Extra '-roeChars','b10,g05'
python tools\burst_sheet.py --chars b10,g05
# 6. 比赛场景（名单默认 a08,g04,b10,g05，-roeRoster 改），exe
.\tools\unity_batch.ps1 -Method RoeFighter.EditorTools.RoeFightScene.Build -Graphics
.\tools\unity_batch.ps1 -Method RoeFighter.EditorTools.RoeFightScene.BuildPlayer -Graphics
```

为了能加新角色，改了这些（都是通用的，没有写死哪个角色）：

- **导入保留旧 GUID**（`import_ripped.py`）：AssetRipper 每次导出都给资源新编号，只还原新角色时，包里还带着工程里已有的公共包（家族头部、公共贴图、皮肤 LUT……）。
  现在工程里已有的资源保留原编号、不重新拷，新文件里指向它们的引用改写成原编号，已有的预制体、场景不会断（这次 116 个）。
  清单（`roe_manifest*.json`）合并：这次没有的角色原样保留（`--fresh-manifest` 才只写这次的）。
- **只处理指定的角色**：`RoeFighterBuilder.BuildAll`、`RoeHumanoid.BuildAll`、`RoeHumanoidClips.ConvertAll` 认 `-roeChars`（不写还是全部）。重建人形预制体会丢掉 `RoeHelperFit` 挂上去的组件，所以加角色时不要碰已有的。
- **包的筛选**：套装自己的 `chara_bare_pc_<id>_nk*` 是 H 场景的骨架和动作，不再拷；`--nude g01_fm` 这种 `fm` 版身体要明确点名才拷。
- **武器材质**：一套有几件武器时材质按武器起名（b10 的斧头 `wp_ax` → `wp_b_10_axe_hd`），按渲染器名里的词去配。g05 战斗里没有武器（低模和技能都没用 `wp_g05`）。
- **挂在手臂、腿上的东西**（`RoeHumanoidClips.Convert`）：人形骨架把肢体的扭转挪到下一节，挂在上臂、前臂上的东西就少转了这一下。
  g05 的两条飘带从手臂一直垂到膝盖，末端差了 0.9–1.2 米。现在这些节点的曲线按人形骨架实际摆出的肢体重算：g05 最大误差 1.7 厘米（手指），b10 1.4 厘米；
  扭转辅助骨也一起变准了（之前 1–3 厘米）。a08、g04 后来也重转了（见下面"第二轮"）。
- **飘带交给布料**：`RoeBoneCloth` 按名字认的链加了一类"飘带"（`riband|ribbon`），参数用头发那套轻的，碰撞体用腿和躯干、骨段整段碰；
  `RoeHelperFit` 不再把飘带当辅助骨拟合（g05 的飘带拟合误差 26–85°，硬跟着手臂反而不对）。g05 现在 5 条飘带由布料模拟，第二轮起游戏自己的动作里也是。
- **站姿基准**（`RoeHumanoidClips.Standing`）：格斗里的鞋底高度、脚怎么站、裙子飘带怎么垂，都从角色的站姿里量。g05 的战斗待机是浮在空中坐着（最低的脚骨离地 23 厘米），
  这时自动改用第一个站着的展示动作（g05 是 `idle_02`），日志里写着用的是哪个。检查图、头像、比赛都用它。
- **光脚**：第一版（`-roeBarefoot g05=1`）只是让站脚不套站姿的高跟站法，结果 g05 在动捕里立在脚尖上（芭蕾那样），第二轮改了，见下面"高跟和平底"。
- **治疗技能当攻击**（`RoeSkillSheet.Action.Blows`）：g05 的技能 2 和超必杀在游戏里是治疗（没有伤害数字，只有 heal、regen 这些效果）。格斗里在效果生效的时刻（2.17 秒、2.80 秒）当成一击，
  所以她的超必杀打中也会让对手掉衣服。有伤害数字的技能照旧。
- **脖子补洞**（爆衣规则表 `nude.fill`）：b10 的高领在夹克上，g05 的金项圈连着长裙片，套装的头部网格只到领子上沿，衣服掉了脖子就是个洞。
  现在从底模的头部子网格里挑出"套装头部没有的三角形"（按三个角的位置比对，0.3 毫米内算同一个点）补进身体，用套装脸的材质，权重照样从附近抄。
  b10 补了 784 个三角形，g05 补了 2,060 个；超过底模这块 30% 时认为不是同一个头，不补（日志里写）。

两人掉什么（`Editor\Burst\b10.json`、`g05.json`）：

| | 第一段 | 第二段 | 第三段 | 第四段 |
|---|---|---|---|---|
| b10 Kart | 枪套、腿上的绑带、枪套挂绳、左臂小包：4,046 个三角形 | 红色短夹克和胸前皮带：6,417 个 | 手套和靴子：5,066 个 | 牛仔短裤和腰带：3,884 个 |
| g05 Luf 女神 | 心形光环和月桂枝：9,042 个 | 肩上的白色披帛：2,072 个 | 金饰（手镯、腿环、脚链、戒指）：4,312 个 | 白色长裙片：7,780 个 |

两人都没有"一直留着"的东西，掉完就是全身（b10 光脚，脚就是底模的脚）。

演示：

- `out\clothes_burst_demo_b10_g05.mp4`：b10、g05 各当一次挨打的一方，对手是另一个，打两局，四段全掉；
- `out\fight_cpu_match_b10_g05.mp4`：电脑对电脑一整场（120 秒，g05 二比零，b10 最后全身）；
- `RoeBurstDemo.Run -roeChars b10,g05 -roeFoes g05,b10`、`RoeFightScene.Record -roeP1 b10 -roeP2 g05` 可以重录。

显示名：a08 INASE，g04 LUF，b10 KART，g05 GODDESS LUF；选人卡片上另有一个词：Valkyrie、Porcelain、Agent、Goddess（`RoeFightScene.DisplayNames` / `Outfits`）。

#### 第二轮：g05 的浮空、飘带和脚，a08、g04 重转（10-03 晚）

用户 compact 之后说"继续"，把上一轮列出的毛病接着修掉。

- **g05 在游戏动作里浮空**（`FighterRig.gameHover`）：她的战斗待机悬在空中，游戏里的受击、倒地、三个技能都从这个高度开始（脚离地 23 厘米），
  基础动作却站在地上：每挨一下打，0.04 秒内被抬起 23 厘米，打完再掉下来。量法：`RoeFightProbe.Hover` 把每个游戏动作单独放一遍，
  记最低的鞋底离地多高（四个人里只有 g05 浮空：受击、倒地、技能开头都是 23 厘米，技能里最高到 1.3 米；另外三人都在 0 附近）。现在：
  - 建场景时量出战斗待机浮多高（`RoeHumanoidClips.Standing` 顺便给出，g05 0.23 米，站着的角色是 0），存在角色上；
  - 游戏动作往下放这么多，但不超过"最低的鞋底踩到地"（动作本身再往上升的部分照留），鞋底已经低于站立高度时（倒地躺下）不动；
  - 还有一条：除了脚，身体任何一根骨头离地不少于 10 厘米（`BodyClearance`）。只按鞋底算时，向后倒地腿往上翻、背往下落，
    臀部被压到地面下 8 厘米；加了这条以后倒地和游戏一样（臀部最低 11 厘米）；
  - 技能例外（`NamedClip.hovers`）：游戏的特效是照悬空的身体放的，所以技能保留游戏的高度，开始时 0.3 秒慢慢升上去（`HoverRise`）。
    格斗里技能在最后一击之后 0.8 秒就收招，这时她常常还在半空（技能 1 收招那一刻脚离地 63 厘米），落回站架的时间按高度放长：
    0.3 秒加每米 0.4 秒，最多 0.8 秒（技能 1 是 0.55 秒，`Fighter.StepSpecial`；不浮空的角色还是 0.3 秒）；
  - `-roeHover g05=0` 回到游戏原样，`-roeSkillsHover 0` 让技能也落地。
- **飘带在游戏动作里也交给布料**（`RoeBoneCloth.AlwaysKinds`，现在只有"飘带"一类）：游戏的关键帧把飘带键得几乎是硬的，受击时一米长的飘带像板子一样甩出去。
  现在飘带在任何动作里都模拟，游戏的关键帧当作它的动画姿势（角度限制让它不离开太远），技能里的甩动还在，只是会弯、会拖在后面。裙子和头发在游戏动作里还是游戏的关键帧。
- **a08、g04 按新的肢体重算重转**（`RoeHumanoidClips.ConvertAll -roeChars a08,g04 -roeKeepPrefab 1`）：重转本来会先重建人形预制体，
  拟合好的辅助骨和裙子组件就丢了；`-roeKeepPrefab 1` 用现有的预制体（同一个骨架定义）只重转动作，`RoeHelperFit`、`FitSkirt` 不用重跑，基础动作里的样子不变。
  回放误差（转换前后每根蒙皮骨的位置差，最大值）：a08 从 1.1–2.3 厘米（小腿、前臂的扭转辅助骨）降到 0.3–1.0 厘米（剩下的在手指上）；
  g04 从 1.2–2.8 降到 0.4–2.6（`idle_02` 的裙片 2.6、技能 2 的手指 2.4 没变）。旧的动作文件备份在 `_work\backup_clips_1003\`。
- **高跟和平底分开**（用户 10-03："luf 神的脚是不是不太对，有的角色是有高跟鞋的，有的角色是没有的，得区分一下"）：
  g05 在动捕里一直立在脚尖上（`RoeFightProbe.Feet`：站架脚掌朝下 72–81°，脚踝离地 15 厘米，脚尖扎进地面 2 厘米），游戏自己的开场动作里她是平踩着的（38°、8 厘米）。
  原因：ROE 的光脚在绑定姿势里就是朝下 72° 的"高跟脚形"，人形骨架的 T 姿势保留了这个角度，动捕的平脚套上来就是脚尖。
  - 怎么分（`Editor\RoeFeet.cs`，建场景时量）：把角色站着的游戏动作（`idle_01` 站着时、`idle_02`、`react_01`、`react_02`）逐帧摆一遍，
    每只脚取着地的帧（脚趾骨离地 2.5 厘米内），按脚踝高度从低往高四分之一处那一帧（最低那一帧不可靠：g04 在 `react_01` 里有一帧鞋跟陷进地面，脚踝只有 9 厘米）。
    脚踝还有 10.5 厘米以上就是高跟：a08 16.1、g04 15.1、b10 14.6 厘米；g05 7.7 厘米，是平底。`RoeFeet.Report` 打印四个人的结果。
  - 高跟的照旧（站脚套站姿的高跟角度）。平底的（`FighterRig.flatFeet`）：动捕的脚先绕脚踝往上抬"绑定姿势的角度减去她站着的角度"（g05 左 34°、右 40°），
    站脚摆成那一帧的样子，鞋底高度也用那一帧的。`-roeFeet g05=flat,b10=heels` 可以手动指定。
  - 第二轮的结果：g05 站架脚掌 43°、脚踝 8 厘米（游戏里 38°、8 厘米），走路的支撑脚也平踩着（对比图 `out\g05_feet_flat.jpg`）。
    但脚趾比游戏里多陷进地面 1.5 厘米，角度陡 5°；第三轮查下去，还有后脚整只朝后站、右脚往下压的毛病，见下一节。
- 对比视频 `out\g05_hover_fix.mp4`（`RoeHoverDemo.Run`，左边游戏原样，右边现在）：g05 被 b10 打中两下、放技能 1、被超必杀打倒、躺地、起身。

```powershell
.\tools\unity_batch.ps1 -Method RoeFighter.EditorTools.RoeFightProbe.Hover -Graphics [-Extra '-roeChars','g05','-roeShots','g05']   # 每个游戏动作离地多高 → _work\hover\hover.txt
.\tools\unity_batch.ps1 -Method RoeFighter.EditorTools.RoeHoverDemo.Run -Graphics                                                   # 对比帧 → _work\hover_demo
python tools\cloth_demo_video.py _work\hover_demo out\g05_hover_fix.mp4 --chars g05 --variants game,floor
```

#### 第三轮：g05 的脚和游戏对齐（10-04）

第二轮留下"脚趾多陷 1.5 厘米、陡 5°"，接着查（`RoeFightProbe.Feet -roeChar g05`，每张照片配一行数字），一共找到四处毛病：

- **被贴地 IK 放到地上的脚，没按站姿摆**：站脚按"脚踝比站立高度高多少"决定套多少站姿（3 厘米内全套，12 厘米以上不套），
  算完之后贴地 IK 才把还差几厘米的脚放到地上。站架里左脚在动捕里高 5 厘米（脚跟抬着），只套了 3/4 的站姿（43°），IK 却把它整个放到地上，
  脚趾就扎进去了。现在 IK 放下多少，就补套多少站姿（`FighterRig.Stand`，IK 之后）。
- **挑站姿那一帧，要整只脚掌着地**（`RoeFeet.SoleDown`）：以前只看脚趾骨着地、脚踝高度，现在还要脚跟那半和前掌那半的皮肤都离地 2.5 厘米以内
  （有这种帧就只在这些帧里挑）。g05 左脚换成 `react_02` 2.10 秒（36°），右脚 `idle_02` 4.80 秒（32°）。
- **站脚的朝向按脚踝的横轴算，不按脚尖**：以前站脚朝哪边，取的是脚尖在地面上的投影。ROE 的脚（光脚和高跟）在绑定姿势里都朝下 67–74°，
  动捕里脚跟一抬，脚尖就过了竖直、指向后方，投影就反了，整只脚掉个头站在地上。现在用脚上下翻转所绕的那根轴（踝关节的横轴）算朝向，
  脚掌朝下多少度都不会翻（脚侧翻到横轴接近竖直时才退回用脚尖）。站架里 g05 后脚的脚尖和膝盖方向从差 146–180° 变成 13–16°；
  着地、膝盖弯着的脚里，脚尖和膝盖反向的从 91 个里 40 个降到 86 个里 1 个（那一个是倒退走的一帧，动捕自己的脚就朝那边）。
- **右脚往上抬，抬反了**（`RoeFeet` 判断转的方向）：平脚在动捕里要先绕脚踝往上抬 36–40°。以前判断方向是"转完以后脚趾骨变高了"，
  可绑定姿势里脚尖朝下 72°，往错的方向转 40° 会越过竖直到 112°，脚趾骨也变高了，两个方向都过。g05 左脚碰巧对了，右脚是往下压了 40°，
  动捕里抬脚、出招时那只脚往后折（脚底朝上）。现在两个方向都试，算连续的角度（过了竖直接着算，大于 90°），选离她站着的角度近的那个。
- 结果（Bandai 包，开场、站架、前进、后退、横移、四个攻击，64 张）：

  | | 站架左脚 | 站架右脚 | 着地脚的脚趾（平均 / 最深） | 陷进地面 2 厘米以上 | 脚跟皮肤离地（平均） |
  |---|---|---|---|---|---|
  | 游戏开场 | 38°，脚踝 8.4，脚跟 1.0，脚趾 −1.0 | 34°，8.0，0.5，−0.6 | | | |
  | 第二轮 | 43°，8.1，2.0，−2.5 | 32°，8.1，5.2，−0.3（脚朝后） | −1.5 / −5.7 厘米 | 33 次 | 3.8 厘米 |
  | 现在 | 36°，7.9，1.5–1.9，−1.2 | 32°，7.9，0.9–1.1，−0.4 | −0.9 / −3.2 厘米 | 3 次 | 2.0 厘米 |

  高跟的三个人只是朝向的算法换了：a08 同一段 128 个脚的数字里只有 3 个变了 1° / 1 厘米以上，都是离地 21–26 厘米的悬空脚。
  对比图 `out\g05_feet_1004.jpg`（左原来立脚尖、中第二轮、右现在：开场、站架、轻拳和重拳收招）。exe 已重新打包，冒烟测试 0 个异常。
- 查的过程中一度以为是辅助骨的毛病：后脚掉头时，脚跟那块皮肤被跟着小腿的辅助骨（`muscle_ankle`、小腿扭转骨）往前带了 6 厘米。
  `RoeFightProbe.HelperHeel` 在她自己的游戏动作里逐帧比过，拟合出来的脚跟皮肤和游戏关键帧只差 0.1–0.2 厘米（最多 1.4），拟合没问题；
  脚摆对以后（站架里膝-踝-脚趾 76–103°，她站着是 112–114°）只剩前腿膝盖压过脚尖时，脚跟皮肤比游戏高 0.5–1 厘米（1.5–1.9 对 1.0），不改。
- 探针多了几项（`RoeFightProbe.Feet`）：脚跟只按脚骨蒙皮的高度、膝-踝-脚趾角、脚尖对膝盖的方向、套站姿之前的脚掌角度和权重、两条腿的关节坐标；
  `-roeSides both` 加拍右侧和正面，`-roeTrack nude_body#6537,nude_body#9396` 逐帧跟一个顶点。

```powershell
.\tools\unity_batch.ps1 -Method RoeFighter.EditorTools.RoeFeet.Report                                                              # 四个人高跟 / 平底、挑中的站姿帧
.\tools\unity_batch.ps1 -Method RoeFighter.EditorTools.RoeFightProbe.Feet -Graphics -Extra '-roeChar','g05','-roePacks','bandai1','-roeSides','both'   # → _work\feet\feet.txt + 照片
.\tools\unity_batch.ps1 -Method RoeFighter.EditorTools.RoeFightProbe.HelperHeel -Extra '-roeChar','g05'                          # 辅助骨拟合对脚跟皮肤的误差
```

### 霞（DOA6）和可插拔物理（10-04）

用户 10-04："doa6中的 Kasumi也加入一个角色，看看乳摇，头发，衣服物理是怎么做的，是否比目前的物理做得更好"，
接着"doa5lr 也调研一下……最好做成可插拔的设计，后续我买到magic cloth也能替换"。全部细节、数字、哪些是猜的，见 [`docs/doa-physics.md`](docs/doa-physics.md)。

- **霞**：
  - 模型：DOA6 的默认服装、头发、脸合成一副骨架。
  - 动作：她自己的 DOA6 站架和四个普通攻击（刺拳、侧踢、上勾拳、高位回旋踢）。
  - 游戏动作：受击、倒地、躺地、开场、胜利、三个技能（空翻踢、剪刀脚、手翻扑击，技能表按肢体速度合成）。
  - 没有游戏的特效和语音。
  - 素材放在 `Assets/DOA/kas`（不入库），定义在 `tools/doa/kas.json`，动作包 `tools/motionpacks/doa6_kas.json`。
  - 名单默认加上 `kas`，没有她的文件时自动跳过。
- **DOA6 的物理**：
  - 胸是体积软体：每边 188 个格点，外面一层壳保持体积，被手臂、大腿顶，网格按格子插值；
  - 马尾、飘带、绳子是骨链；
  - 刘海是摆动骨；
  - 裙子、袖子是粗网格布（霞的默认服装没有）。
  - 读成 `Assets/DOA/kas/physics.json`（`tools/doa6_physics.py`），由 `DoaPhysicsBuilder` 挂到预制体上（`RoeDoaRig`），`RoeDoaPhysics` 每步模拟。
  - 骨架脚本驱动的扭转辅助骨（`RF_*`）也补上了（`RoeDoaRig.DriveHelpers`）。
- **DOA5LR**：
  - 胸不模拟，按每套衣服选的预设表驱动 7 根骨；
  - 衣服头发是"一个质点就是一根骨头"的全套弹簧网（竖、横环、斜拉、隔一根）；
  - 刘海是姿势混合。
  - 只调研，没做成解算器。
- **可插拔**：
  - F4 换"物理方案"，每个方案给胸、身体、头发、裙子、飘带、链子各指定一个解算器（骨骼布料 / 10-02 旧版 / DOA6 / Magica Cloth 2）。
  - 解算器在某个角色身上做不了的类别交给骨骼布料。
  - 默认 `auto`：霞用 DOA6 的，ROE 角色用骨骼布料。
- **比我们好吗**：
  - DOA6 的胸确实更丰富（会被挤、保持体积）；
  - 头发飘带和我们同一类，但多一个往动画姿势拉回的量，更安静。
  - 软体参数的含义都是猜的：照现在的读法，平常晃得自然（平均偏 0.4–2 厘米），猛的动作会甩出 6–9 厘米，罩杯下缘和胸衣之间有一条细缝。
- 顺手修的老毛病：F4 从 44b174e 起只改了屏幕提示，实际一直是骨骼布料（场景把没设的 `clothBackend` 存成了空字符串）。

```powershell
.\tools\unity_batch.ps1 -Method RoeFighter.EditorTools.DoaFighter.Build -Extra '-roeDoa','kas'                         # 预制体 + 物理（DoaPhysicsBuilder）
.\tools\unity_batch.ps1 -Method RoeFighter.EditorTools.RoeClothDemo.Run -Graphics -Extra '-roeChars','kas','-roeCloths','magica_style,doa6,off','-roeView','chest'
python tools\cloth_demo_video.py _work\cloth_demo out\kas_physics_chest.mp4 --chars kas --variants magica_style,doa6,off
.\tools\unity_batch.ps1 -Method RoeFighter.EditorTools.DoaPhysicsProbe.Soft -Extra '-roeSteps','30,120,620'              # 软体格点偏离多少、被谁推
```

### 选人界面（10-03 晚）

- 场景里放着名单上的所有人（`-roeRoster`，默认 `a08,g04,b10,g05`），开局先进选人界面；没选上的收起来（不显示、不计算）。
- 下面一排卡片，头像是建场景时在影棚里拍的（`Generated\<id>\<id>_portrait.png`）；选中的两人站在场上，播她们的展示待机。上方写着轮到谁选。
- 1P 是人、2P 是电脑（默认）：1P 先选自己的，确认后接着替电脑选；两边都是人就各选各的；两边都是电脑（F1 + F2）就随机选好直接开打。
- 两边选同一个角色时，复制一份那个角色（第一次用到时复制，之后留着）。
- 比赛结束按回车回到这里（上一场的两人还选着，确认两次就是再来一场）；F7 随时回来；F3、F4 在这里按也有效（两人换上新动作包 / 新布料）。
- `ROEFighter.exe -roeP1 b10 -roeP2 g05` 预先选好这两人；`-roeSelect 0` 跳过选人直接开打。
- 演示 `out\select_screen_demo.mp4`（`RoeFightScene.SelectDemo`：批处理里没有键盘，用脚本按同样的步骤换卡片）：1P 从 INASE 换到 LUF（两边都是 LUF 时复制了一份）再到 KART、确认，替电脑从 LUF 换到 INASE 再到 GODDESS LUF、确认，开打。
- 坑：拍头像时开了景深，而影棚的后期设置文件（`Settings\RoeStudioVolume.asset`）比赛场景也在用，焦点 1.45 米的景深就留在了比赛里，exe 整个画面是虚的（批处理录像没受影响）。现在头像不开景深。

## 已知问题和待定的事

- 布料只在基础动作里生效（飘带除外，见"第二轮"）；游戏自己的技能里裙子和头发是手调的关键帧。裙子的参数按 g04 和 a08 量过调过（见"布料"一节）；
  头发、链子、胸还是 Blender 插件的预设。g04 的一条细装饰线在出招时偶尔一帧转 20–30°。
- **裙子**：用户 10-02 晚看过 `out\g04_skirt_fix.mp4` 说"不行，还是有问题"。调研 Magica Cloth 2 之后照它的做法重做了（"布料"一节最后一条，
  `docs/magica-cloth-2.md` 第 9 节）：裙子的动画姿势跟腿走、一片布连成网、惯性分层，F4 可以和旧版对比。等用户看新视频 `out\skirt_magica_style.mp4`。
  之后用户说屁股附近的后片翘起来一点，原因和改法见"布料"一节"屁股附近翘起来"，对比视频 `out\skirt_rest_compare.mp4`。
  10-03 用户问"能自然下垂么"，改成贴着身体自然下垂（"布料"一节"自然下垂"，F5 切换），对比视频 `out\skirt_drape_compare.mp4`。
  还没做：横边在踢腿时还会被拉长（最多 40%）；三角弯曲约束（Magica 网格模式有）没加；g04 前进时后片下摆的毛边还会碰到脚后跟（约 1% 的顶点、1.5 厘米）。
- **TODO 裙子还有一小部分穿模**（用户 10-03 看过 `out\skirt_drape_compare.mp4`："裙子还是不完美，一小部分穿模了，这个先留个todo"）。量到的地方
  （`RoeFightProbe.ButtGap` 的"裙子网格陷进皮肤"，Bandai 包）：g04 后片屁股下方那节（`Skirt_Back_02`）的边缘约 1 厘米，前进时下摆毛边碰脚后跟约 1.5 厘米，
  后退时前片下半（`Skirt_front_02~04`）1–2 厘米；a08 站架、前进、后退为 0，踢腿、侧步、出拳还没逐帧量过。根子：基准姿势已经按皮肤挂好，
  但布料模拟只拿每节骨的尾端 / 骨段去碰腿部胶囊，宽的裙片、厚的毛边在模拟里还会被腿带进去，单条链也没法顺着屁股弯过去。可以试：
  ① 模拟里也用皮肤表面（`RoeBodySurface`）和每节布的采样顶点做碰撞，不只用胶囊；② 一片宽布拆成几条虚拟链，或者补三角弯曲约束；
  ③ 换成按网格顶点模拟（Magica 的 MeshCloth / 代理网格）；④ 先把 `SkirtSwing` 加上皮肤穿透的逐帧统计，所有动作都量一遍再定。
  10-03 拆了 DOA6 的数据：它的裙子就是③的做法。
  - 每块布只模拟一张粗的控制点网格：不知火舞每块 5 列 × 6–11 行，霞的整圈裙子是 20 × 10 的环。
  - 看得见的布料网格每帧从这张网插值出来，再沿布面法线加厚度。
  - 碰撞体按布分组，每块布只碰附近 6–16 个。

  可以先做便宜的一半：在已经连成网的裙骨上，给布料顶点按网格法线加厚度偏移。详见 ripper_tpose `docs/doa-clothing-and-cloth.md`。
  Magica 适配器是照手册写的草稿，插件装上后第一次编译、试跑时可能要改。
- **爆衣**（10-03，`docs/clothes-burst.md`，"爆衣"一节）：
  - 第一、二步做完了：
    - 超必杀命中、KO 各掉一段，决胜的 KO 全掉，整场保留，F6 开关；
    - 家族裸体底模代替套装皮肤，两人能掉到全身（g04 留着鞋）；
    - 规则表是每个角色一份 JSON，代码对所有角色通用。
  - 还没做：
    - 第三步"打哪破哪"：着色器按命中点挖洞；
    - 烧边；
    - 碎布在空中飘（现在是刚体快照，落地才摊平）；
    - 掉了的裙片停掉它的布料链。
  - 用户 10-03："做完这个demo，还希望把 Riseoferos 其他的角色也加到这个游戏中，可以爆衣，可以适配动作和衣服"，之后再讨论。
    加角色的现成做法见"爆衣"一节最后。要补的主要是每个角色的规则表，以及"哪里缺皮肤"的测量。
- **新角色**（10-03 晚）：
  - g05 的技能 2 和超必杀是把游戏的治疗时刻当成攻击，不是游戏的本意，用户可以改成"给自己回血"；
  - b10 用的是动作包的拳击动作，手里的斧头和双枪只在技能里出现；
  - a08、g04 的辅助骨拟合（`RoeHelperFit`）还是用旧动作拟合的（旧动作里扭转辅助骨差 1–3 厘米），没重跑，基础动作里的样子因此没变。
- 动作包现在有 `bandai1`（默认）和 `accad_male2` 两个，买来的 Unity 动作照"动作包"一节的 JSON 接进来。`accad_male2` 是右脚在前的拳击架势，
  g04 举着大扇子打拳击架势时扇子挡在身前；只有四个攻击键，ACCAD 里还有勾拳、上勾拳、侧踢、前踢、防御、闪避、胜利动作没用上，CMU 的空手道也可以补进来。
- 一个角色播另一个角色的技能时，辅助骨用的还是原角色动作里的数值；`RoeHelperRig` 也可以用在这里，还没接。
- 布料物理：游戏用的是 Magica Cloth 2，但只在大厅模型上模拟头发和饰物（设置都能取出来，`tools\magica_survey.py settings`）；
  战斗里的裙子是手调动画，所以买了插件也没有现成的裙子设置可以照搬（见 `docs/magica-cloth-2.md`）。
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
- **霞（DOA6，10-04）**：
  - 软体参数的读法要在游戏里实拍对照；
  - 侧踢起手胸被甩起 6–9 厘米；
  - 罩杯下缘和胸衣之间有细缝；
  - 手掌片碰撞体比游戏的厚；
  - 肘、膝、胯的修形骨和前臂扭转没驱动；
  - 开场从 2.5 米高处跳下，落地时胸顶到上限；
  - 没有游戏特效和语音。

  见 `docs/doa-physics.md` 第 9 节。
- KO 镜头不避墙：KO 发生在场地边上时，镜头会在铁栅栏后面（`out\kas_fight_test.mp4` 第 57 秒）。

## 环境

- Unity 6000.4.12f1，装在 `E:\tools\Unity\Hub\Editor\6000.4.12f1`；Unity Hub 3.22（winget 装的）。选 6000.4 是因为 UFE 2.7.3 在 Asset Store 上要求不低于 6000.4.1。
- 这台电脑直连 Unity 的下载服务器会被转到中国站（国际版安装包 404），只能走环境变量里的代理，而代理单连接只有约 30 KB/s。`tools\fetch_parallel.py` 用 64 个连接分段下载（约 1.8 MB/s），`fetch_fill.py` 补最后几个慢块。
- Git Bash 里的 curl 访问 127.0.0.1 也会走代理而失败，本机服务用 Python（`ar.py` 里关掉了代理）或 PowerShell 访问。
