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
| UFE 对战 | 等 UFE 2 到货 |

## 目录

```
Assets/RoeFighter/Shaders/     五个着色器和公共代码
Assets/RoeFighter/Editor/      编辑器脚本：建工程设置、拼角色、拍图、转动作、各种检查
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

游戏的着色器代码还原不出来，只能拿到每个着色器的参数表（名字、范围、开关）。这里按参数表重写了五个 URP 着色器，**参数名和游戏的一样**，所以游戏的材质文件原样能用，`import_ripped.py` 只把材质里的着色器引用换成这里的。

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

## 已知问题和待定的事

- 套用别人的动作时：头发、裙子、胸不会动（要接布料物理）；扭转辅助骨骼没有驱动（要写一个按肢体旋转比例跟随的小脚本）。这两件等 UFE 的基础动作到手再做，才有东西可对照。
- 布料物理：游戏用的是 Magica Cloth 2，每套服装的设置（哪些骨骼、重力、阻尼、身体碰撞体）都能取出来。买了这个插件就能直接照搬；不买就自己写弹簧骨骼。
- a08 的剑在基础动作里怎么处理还没定：现在是跟着骨盆走。可选的做法是基础动作时隐藏、放技能时出现。
- 头发比游戏里的略亮，眉毛偏淡，还要对着参照视频调。
- 战斗动作里脚尖会短暂穿到地面以下 10–20 厘米，这是原动作就有的。

## 环境

- Unity 6000.4.12f1，装在 `E:\tools\Unity\Hub\Editor\6000.4.12f1`；Unity Hub 3.22（winget 装的）。选 6000.4 是因为 UFE 2.7.3 在 Asset Store 上要求不低于 6000.4.1。
- 这台电脑直连 Unity 的下载服务器会被转到中国站（国际版安装包 404），只能走环境变量里的代理，而代理单连接只有约 30 KB/s。`tools\fetch_parallel.py` 用 64 个连接分段下载（约 1.8 MB/s），`fetch_fill.py` 补最后几个慢块。
- Git Bash 里的 curl 访问 127.0.0.1 也会走代理而失败，本机服务用 Python（`ar.py` 里关掉了代理）或 PowerShell 访问。
