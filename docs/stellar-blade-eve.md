# 剑星（Stellar Blade）的 Eve 加入格斗（10-05）

用户 10-05："另外把剑星里的 eve 的也加个角色进来，物理能用剑星自己的就用自己的，次选项才是用 magic cloth2"。

结论先说：

- Eve（7 代潜降服，`CH_P_EVE_09`，角色 `eve09`）进了对战，选人界面有她。
- 她身上的物理**全部照剑星游戏自己的**做，参数从游戏包里直接读出来，没有用 Magica Cloth 2：
  - 胸、臀、大腿、护腕：UE4 自带的 **SpringBone** 弹簧骨；
  - 前发、鬓发、马尾根部、领带：**KawaiiPhysics** 插件（和 Fiona 那套同一个插件，算法已经移植过）；
  - 马尾后段、背后的半透明披风：**PhysX 刚体链**（UE 的物理资产），Unity 也是 PhysX，直接照搬质量、阻尼、关节角度限制和碰撞体。
- F4 多了一个方案 `stellar`（剑星自己的物理）；`auto` 时 Eve 自动用它。
- 动作暂时用现有的动作包和 UFE 2 的受击、技能（和 Fiona 第一版一样）；她游戏里自己的动作（约 4100 段，含剑技连招）是下一步。

## 1. 模型从哪来

素材是 ripper_tpose 早就导好、归档在 `E:\game_export\StellarBlade\Eve\blend\CH_P_EVE_09\` 的 `.blend`
（`scripts/stellarblade` 的 `export_outfit.ps1` / `validate_eve.py`：服装身体 + Face_003 头 + 发型 + 马尾合在一个骨架 `Eve_Armature` 上）。

| 步骤 | 工具 | 做什么 |
|---|---|---|
| 1 | `tools/sb_fbx.py`（Blender 3.6） | 导出 FBX：Biped 骨骼名的连字符换成空格（`Bip001-L-Clavicle` → `Bip001 L Clavicle`，人形骨架按 ROE 的名字认）；她在 `.blend` 里朝 +X，转到 −Y（Unity 的前方）；厘米 → 米；两条马尾只留长的（游戏里同时只显示一条）；眼睛的虹膜在节点里是两张图混合，烘成一张；另写 `bones.json`（骨骼名、父骨、位置、蒙皮顶点数） |
| 2 | `tools/sb_textures.py` | 归档的 `.blend` 只接了颜色图。这里按**游戏自己的材质实例**（CUE4Parse 命令行读出来）取每个材质真正用的图：颜色、法线（UE 是 DirectX，绿通道翻转）、ORM（R 环境光遮蔽、G 粗糙度、B 金属度 → URP 的遮罩图）、发光（服装上的绿色光线）；头发颜色用材质里的发根 / 发梢颜色；眼睛外层的泪膜、阴影壳隐藏 |
| 3 | `tools/sb_physics.py` | 读游戏的物理设置（下一节），写 `kawaii.json` 和 `sbphysics.json` |
| 4 | Unity：`SbFighter.Build` | 材质、模型、人形骨架（和 Fiona 共用 `VdfFighter.BuildModel`，加了发光贴图）；把物理设置挂到预制体上；检查游戏的碰撞体和关节落在她身上的位置 |

坑：

- **Blender 里转骨架不刷新姿态**：`Armature.transform()` 只改了静止姿态，不打更新标记，导出器写的骨骼节点还是原来的姿态。
  第一次导出，转 0°、−90°、180° 三个 FBX 在 Unity 里一模一样，她朝 −X。现在转完对骨架和网格 `update_tag()` 再更新。
- 归档 `.blend` 里头发的颜色是近乎黑蓝的常量；游戏材质 `MA_CH_Hair` 是发根 / 发梢两个颜色乘亮度，改用游戏的。

## 2. 游戏里 Eve 的物理是什么

### 2.1 怎么读出来的

剑星是 UE 4.26 的 IoStore 包，属性是无版本序列化，要配社区的映射文件：

- 映射：`E:\game_export\StellarBlade\_meta\mappings\StellarBlade_1.1.0.usmap`；
- 工具：`E:\tools\cue4parse_cli_ff7\cue4parse.exe`（FF7 那次打过补丁的 CUE4Parse 命令行），`-g GAME_StellarBlade -f json`；
- 读出来的 JSON 缓存在 `_work\sb\research\json\`（不入库）。

和 Fiona（Vindictus，没有映射文件，手工解析字节）不同，这里每个动画节点、物理资产的所有字段都直接是 JSON。

```powershell
$G = 'D:\Program Files (x86)\Steam\steamapps\common\StellarBlade\SB\Content\Paks'
$M = 'E:\game_export\StellarBlade\_meta\mappings\StellarBlade_1.1.0.usmap'
E:\tools\cue4parse_cli_ff7\cue4parse.exe -i $G -g GAME_StellarBlade -m $M -f json -o _work\sb\research\json -y `
    -p 'SB/Content/Art/Character/PC/CH_P_EVE_01/Blueprints/CH_P_EVE_01_AnimBP_New*'
```

注意 `-p` 要带通配符（`名字*` 或 `名字.*`），写死包名会报"No matches"；多个 `-p` 一起导时偶尔有并行写文件的异常，单个导就好。

### 2.2 都在哪

| 资产 | 是什么 | 用在她身上的 |
|---|---|---|
| `CH_P_EVE_01/Blueprints/CH_P_EVE_01_AnimBP_New` | 主动画蓝图 | 27 个 SpringBone 节点（09 这套衣服有其中 8 根骨）、1 个 KawaiiPhysics 节点（马尾根）、2 个 Constraint 节点（胸骨跟随假骨 `Dm-*-Breast-Point`）、11 个 ControlRig（脚 IK、看向、游泳、自拍、受击停顿等玩法用的） |
| `00_HR/EVE_HR_01/EVE_HR_01_AnimBP` | 头发网格的后处理蓝图 | 6 个 KawaiiPhysics 节点：前发 4 条（`Ab-HairVF*`）、鬓发 2 条（`Ab_Hair_ex*`），碰撞胶囊在 `KawaiiLimitsData_Head` |
| `CH_P_EVE_09/Blueprints/CH_P_EVE_09_AnimBP` | 这套衣服的后处理蓝图 | 2 个 KawaiiPhysics 节点：领带（`Ab_NeckAcc01..06`）；1 个 ControlRig `BtoB_CtrlRig`（见 2.6） |
| `00_HR/EVE_HR_01/EVE_HR_01_Tail_PhysicsAsset` | 马尾网格的物理资产 | 马尾 9 节刚体 + 关节 |
| `CH_P_EVE_09/CH_P_EVE_09_PonytailPhysicsAsset` | 这套衣服给马尾的碰撞体 | 头、脖子、脊柱、骨盆、大小腿、上臂、前臂的形状 |
| `CH_P_EVE_09/CH_P_EVE_09_Physics` | 这套衣服身体的物理资产 | 披风左右各 7 节刚体 + 关节，身体各骨的运动学碰撞体 |
| 角色蓝图 `CH_P_EVE_01_Blueprint` | 组件设置 | 马尾是单独的 `SBPonytail` 组件，`bLocalSpaceSimulation: true`；`AdditiveMasterBoneArray` 列出马尾碰撞要跟随的身体骨骼（正好是上面那套碰撞体的骨骼）；身体组件也是 `bLocalSpaceSimulation: true` |

### 2.3 SpringBone（UE4 自带的弹簧骨）

09 这套衣服有骨的 8 个（其余 19 个是别的衣服的骨：腹部、上臂、小腿、兜帽等）：

| 骨骼 | 最大位移（cm） | 刚度 | 阻尼 | 平移 | 旋转 |
|---|---:|---:|---:|---|---|
| `Ab-L/R-Breast`（胸） | 1 | 400 | 20 | xyz | — |
| `Ab-L/R-Hip-Reg`（臀） | 2 | 400 | 25 | xyz | — |
| `Ab-L/R-Thigh-Tw0`（大腿扭转骨） | 1 | 300 | 30 | xyz | — |
| `Ab_Acc_ForearmL/R`（护腕） | 0.5 | 400 | 30 | xyz | x、z |

- 全部 `bLimitDisplacement`、`ErrorResetThresh 255`、`Alpha 1`。
- 剑星改过这个节点：多了 `bUseLocalSpace`（全是 true）、`BaseSpaceBoneName`（全是 None）、`AverageVelocityFrameCount`（4）、`HistoryBoneVelocity`。
  源码在游戏的 exe 里，读不到；这里按字面意思理解成"在角色自己的空间里模拟"（走路、被打退不带动，只有身体自己的动作带动），速度平均没有照做。
- 算法照 UE 4.26 的 `AnimNode_SpringBone`：固定 120 Hz 子步，误差 × 刚度 − 速度 × 阻尼；阻尼大于 1/dt 时按比例缩小；
  每步速度不超过 `ErrorResetThresh`；位移限制在 `MaxDisplacement` 的球里；旋转按允许的轴取父骨→骨的偏转。

### 2.4 KawaiiPhysics（9 个节点）

剑星用的 KawaiiPhysics 比 Fiona（Vindictus 2024）的老一代：有 `TeleportDistance/RotationThreshold`，还没有模拟空间、骨骼约束这些字段；
另外有剑星自己加的 `EnterVehicleScale`、`MasterMesh*Limits`（都没用上）。核心算法相同，直接用 Fiona 时移植的 `RoeKawaiiPhysics`。

| 节点 | 根骨 | 排除 | 阻尼 | 刚度 | 半径（cm） | 碰撞 |
|---|---|---|---:|---:|---:|---|
| 主蓝图 | `Ab-TL-HairB01`（马尾根） | `Ab-TL-HairB03` 及以下 | 0.1 | 0.05 | 3 × 曲线 `KPhysics_Dn_Cv01_2` | 21 个胶囊（脊柱、手臂、腿、头、锁骨） |
| 头发 ×4 | `Ab-HairVFLL01` / `VFL01` / `VFR01` / `VFRR01`（前发） | — | 0.1 | 0.05 | 1 | `KawaiiLimitsData_Head`：头部 2 个胶囊（挂在头发网格的根骨） |
| 头发 ×2 | `Ab_Hair_exL01` / `exR01`（鬓发） | — | 0.1 | 0.05 | 1 | 同上 |
| 衣服 ×2 | `Ab_NeckAcc01`（排除 04）、`Ab_NeckAcc03`（领带上下两段） | — | 0.1 | 0.05 | 3 | 6 个胶囊（脊柱、上臂） |

- 全部 `WorldDampingLocation/Rotation 0.8`、重力 0、目标帧率 60。
- 风（`bEnableWind`、`WindScale`）没做：擂台上没有风。
- 改了一处：节点改成逐个模拟、逐个写回（UE 动画图就是一个接一个跑的）。领带两段节点首尾相接，后一段的根要先看到前一段的结果；Fiona 的节点互不相干，不受影响。

### 2.5 PhysX 刚体链

马尾：`Ab-TL-HairB01`、`B02` 交给上面的 KawaiiPhysics（它排除了 `B03` 及以下），从 `B03` 往下 7 节是刚体（马尾网格自己的物理资产）：

| 骨骼 | 质量（kg） | 线阻尼 | 角阻尼 | 关节（锥角 1 / 锥角 2 / 扭转） |
|---|---:|---:|---:|---|
| `B03` | 0.6 | 5 | 1 | 45° / 45° / 15°（连 `B02`） |
| `B04` | 0.5 | 5 | 1 | 45° / 45° / 15° |
| `B05` | 0.3 | 5 | 1 | 同上 |
| `B06` | 0.1 | 5 | 1 | 同上 |
| `B07` | 0.08 | 5 | 1 | 同上 |
| `B08` | 0.06 | 5 | 1 | 同上 |
| `B09` | 0.04 | 5 | 1 | 同上 |

（45° 是 UE 的默认值：无版本序列化不写默认值，读不到就是默认。）碰撞体：这套衣服的马尾碰撞集（头球 + 头胶囊、脖子、脊柱、骨盆、大小腿、上臂、前臂）加上马尾自己的 `B01`、`B02`。

披风：左右各 7 节 `Ab_CapeL/R01..07`，挂在上臂扭转骨 `Ab-L/R-UpperArm-Tw1` 下面；每节 20 kg、线阻尼 5、角阻尼 10，关节锥角 10° / 10°、扭转 10°；
碰撞体是这套衣服物理资产里身体各骨的运动学刚体（领带、胸、臀也在里面）。

搬到 Unity：

- 每个角色一个**自己的物理场景**（播放时 `SceneManager.CreateScene(..., LocalPhysicsMode.Physics3D)`，编辑器里录像时用预览场景），
  刚体放在角色自己的坐标系里，所以等于游戏的 `bLocalSpaceSimulation`：人整体移动不甩，身体动作才甩。格斗每步走一次 `PhysicsScene.Simulate`。
- 运动学刚体每步移到骨骼当前的位置（`MovePosition` / `MoveRotation`），模拟完把刚体的旋转写回骨骼。
- 关节用 `ConfigurableJoint`：主轴 = UE 的扭转轴，副轴 = UE 的 Swing2 轴，第三轴 = Swing1；位移全锁；投影 5 cm（UE 约束的默认）。
- UE 物理资产编辑器会把"静止时就碰在一起"的刚体对排除掉（存在碰撞禁用表里，读不到），这里在建的时候的姿势上用 `Physics.ComputePenetration` 找出来排除（绑定姿势 20 对，格斗里按站架建，19 对）。
- 每个关节按游戏的约束框架（Frame1 在子骨空间、Frame2 在父骨空间）把子刚体摆到静止位置再建关节，所以关节的零点就是游戏的静止姿态。

轴向：UE 骨骼空间的 (x, y, z) 是她骨骼里的 (−x, −y, z)（和 Fiona 一样），检查结果：

> 21 个关节，游戏约束框架算出的子骨静止位置和她骨骼的实际位置：全部 0.0 厘米、0 度。

### 2.6 没搬的

| 东西 | 是什么 | 为什么没搬 |
|---|---|---|
| `BtoB_CtrlRig` | 衣服蓝图里的 Control Rig（RigVM，12 个函数：取骨骼变换、向量长度、浮点重映射、累加插值、设变换） | 量手臂到胸的距离，手臂压到胸时把胸骨挤开。CUE4Parse 能把字节码和内存都读出来，以后可以像 Fiona 的程序化骨骼那样解；格斗里影响小 |
| 衣服上的 NvCloth 布料 `CH_P_EVE_09_skin_Eve01_AX1_L_Clothing_0` | 网格布料（APEX 那一套） | 只是领口附近很小的一片 |
| 马尾组件 `Root` 上的形状 | 马尾网格的根骨（挂在头的插槽上）上一个头部胶囊、衣服碰撞集里一块 7.5 cm 厚的大平板 | 这个根骨不是身体的骨骼，位置要从马尾骨架换算；头部已有衣服碰撞集里的头球和头胶囊。那块平板看起来是挡马尾甩到身前的，后面要是看到马尾穿到身前再加 |
| `PhysTransformStabilizationLerpValue 0.2` | 剑星自己加的组件参数 | 源码在 exe 里 |
| 扭转骨、修正骨（`Ab-*-Tw0/1`、`Ab-*-Elbow`、`Ab-*-Knee`、`Dm-*` 等） | 3ds Max 的辅助骨 | 游戏里是逐帧打在每段动画上的关键帧，不是运行时算的（主蓝图里没有驱动它们的节点）。人形动作不带这些骨，所以要像 ROE 角色那样用她游戏里的动作片段拟合（`RoeHelperFit`），等导她自己的动作时一起做 |

## 3. 效果

物理对比录像，每段 11.6 秒：站架、前进、后退、两次侧步，再按 A、C、D、B 四个攻击（刺拳、直拳、回旋踢、挥砍）；左：剑星自己的（`stellar`），中：我们的骨骼布料（`magica_style`），右：关。

- `out\eve09_physics_front.mp4`：正面全身；
- `out\eve09_physics_back.mp4`：背后全身（演示工具新加的 `-roeView back`；原来的 `backright` 只拍胯部特写，看不到整条马尾）。

看到的：

| | 剑星自己的 | 我们的骨骼布料 | 关 |
|---|---|---|---|
| 马尾 | 走路时贴着背小幅摆；回旋踢、转身时整条甩出去，再落回背后 | 会摆 | 停在建模时的样子，斜着支在背后 |
| 披风 | 自然垂下，跟着腿和胯摆，踢腿时被带起来 | 没认出来，一直是僵的 | 僵 |

胸、臀、大腿、护腕的弹簧骨，游戏设的位移上限只有 0.5–2 厘米，画面上几乎看不出来。

电脑对电脑 `out\fight_cpu_match_eve09.mp4`：对 a08，116 秒，Eve 2:0 赢。抽查整场的画面，马尾和披风没有甩飞、没有卡在奇怪的位置。
画面里大团的黑灰色拖影是命中火花（所有对局共用的 g04 技能命中特效），不是她的物理。

## 4. 用法

```powershell
# 1. 模型（Blender 3.6，读归档的 .blend，不保存）
& 'D:\Program Files\blender-3.6.15-windows-x64\blender.exe' -b --factory-startup `
    'E:\game_export\StellarBlade\Eve\blend\CH_P_EVE_09\Eve_CH_P_EVE_09.blend' --python tools\sb_fbx.py -- Assets\SB\eve09 eve09
# 2. 贴图（游戏材质实例 → URP 的图，结果 unity.json）
python tools\sb_textures.py Assets\SB\eve09
# 3. 物理（游戏的蓝图、物理资产 → kawaii.json、sbphysics.json）
python tools\sb_physics.py Assets\SB\eve09
# 4. Unity：建角色，再重建对战场景
.\tools\unity_batch.ps1 -Method RoeFighter.EditorTools.SbFighter.Build -Graphics -Extra '-roeSb','eve09'
.\tools\unity_batch.ps1 -Method RoeFighter.EditorTools.RoeFightScene.Build -Graphics
# 物理对比录像：剑星自己的 | 骨骼布料 | 关
.\tools\unity_batch.ps1 -Method RoeFighter.EditorTools.RoeClothDemo.Run -Graphics -Extra '-roeChars','eve09','-roeCloths','stellar,magica_style,off','-roeView','back','-roeOut','E:\code\othercode\roe_fighter_unity\_work\eve_cloth_back'
python tools\cloth_demo_video.py _work\eve_cloth_back out\eve09_physics_back.mp4 --chars eve09 --variants stellar,magica_style,off
# 电脑对电脑
.\tools\unity_batch.ps1 -Method RoeFighter.EditorTools.RoeFightScene.Record -Graphics -Extra '-roeP1','eve09','-roeP2','a08','-roeSeconds','120','-roeOut','E:\code\othercode\roe_fighter_unity\_work\fight_eve09'
python tools\make_video.py _work\fight_eve09 out\fight_cpu_match_eve09.mp4
```

- 素材在 `Assets/SB/eve09/`（不入库），格斗定义 `tools/sb/eve09.json`。
- 游戏包里读的东西只缓存在 `_work\sb\`。
- 换别的衣服：`sb_fbx.py` 换 `.blend`，`sb_physics.py --outfit CH_P_EVE_xx`，`sb_textures.py --mi-dir Art/Character/PC/CH_P_EVE_xx/Materials`。
