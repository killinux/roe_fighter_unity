# Fiona（Vindictus，PCF_005）加入战斗（2026-10-04）

用户 10-04："再加入一个角色吧，vindictus里的fiona，用 PCF_005 这个版本，加入进去，注意衣服和头发效果"。

PCF_005 是白色短裙礼服，裙摆一圈荷叶边；头冠、领口、两边上臂带羽毛；短发（波波头）；高跟鞋。
素材来自本机装的《Vindictus: Defying Fate》2024-03 预览版（只读，不入库，只在本机自己用）。

## 结论

- **衣服和头发用她自己游戏的物理。**
  - 游戏里裙子、羽毛、头发、胸全部是 KawaiiPhysics（一个 UE 的骨链弹簧插件，开源，MIT）。
  - 每条链的参数是从游戏文件里读出来的。
  - 算法照插件 v1.14 的源码逐行搬到 Unity（`RoeKawaiiPhysics`），F4 的方案叫 `kawaii`。
  - 默认 `auto` 下她就用这一套。
- **动作用她游戏里自己的**（10-04 夜，第 4 节）：长剑加盾。
  - 从游戏包导出 191 段，转了 87 段。
  - 站架、走跑、四个攻击、三个技能、受击、击倒、躺地、起身、开场、胜利全换成她的。
  - 剑和盾也从游戏里拿来挂在手上，攻击按剑身判定命中。
  - 10-05：剑和盾跟着游戏的动作转（举盾反击时盾朝前，胜利时反手握剑），和游戏比盾的朝向误差从平均 26° 降到 5.5°。
  - MetaHuman 骨架比 Unity 人形骨架多出的脊柱节和扭转骨，在运行时补回去（`RoeUeRig`）。
- **高跟鞋**：MetaHuman 身体的绑定姿势是平脚站，鞋跟比前掌低 5.3 厘米。
  - 建人形骨架时把脚尖往下压 32°，鞋跟和前掌一样高，脚趾再抬回平的。
  - 这样平脚的动捕套上来她也踩在鞋跟上（ROE 角色的绑定姿势本来就是高跟，见 README "手臂和腿"）。

## 1. 模型进 Unity

```powershell
# 1. Blender 3.6：归档的 PCF_005.blend（ripper_tpose scripts/vindictus 拼好的：游戏的 UE5 骨架，6 个部件，材质照游戏的材质实例重建）
#    → 一个 FBX（米）+ materials.json（每个材质用了哪些图、什么颜色调整），.blend 本身不保存
"D:\Program Files\blender-3.6.15-windows-x64\blender.exe" -b --factory-startup E:\game_export\Vindictus\Fiona\blend\PCF_005\PCF_005.blend --python tools\vdf_fbx.py -- E:\code\othercode\roe_fighter_unity\Assets\VDF\fio005 fio005
# 2. Unity 用的贴图：颜色调整烘进底色图，ARM 图换成 URP 的金属 / 遮蔽 / 光滑度图，法线图翻绿
python tools\vdf_textures.py Assets\VDF\fio005
# 3. 游戏的 KawaiiPhysics 参数（第 3 节）
python tools\vdf_kawaii.py _work\vdf\research\kawaii_params.json Assets\VDF\fio005\kawaii.json
# 4. Unity：材质、预制体、人形骨架、物理数据
.\tools\unity_batch.ps1 -Method RoeFighter.EditorTools.VdfFighter.Build -Extra '-roeVdf','fio005'
.\tools\unity_batch.ps1 -Method RoeFighter.EditorTools.DoaFighter.Stills -Graphics -Extra '-roeDoa','fio005'    # 静帧检查
```

- **骨架**：1506 根骨骼。
  - 身体是 MetaHuman 的（pelvis、spine_01–05、neck_01/02、head……）。
  - 脸有 620 根 FACIAL_ 骨骼，保持中性表情。
  - 外加裙子 14 条链、头发 46 条、羽毛和耳环、胸的 74 根。
  - `RoeHumanoid.MapBones` 加了 UE 的骨骼名表：Spine = spine_01，Chest = spine_03，UpperChest = spine_05，脚趾 = ball。
  - 中间的 spine_02、spine_04、neck_02 和所有扭转、修正骨骼跟着父骨骼走。
- **单位和朝向**：.blend 是厘米，导出时 ×0.01、缩放烘进去，在 Unity 里是 1.87 米高（含头冠羽毛）、面朝 +Z、骨骼没有缩放。
- **材质**（`vdf_textures.py` → `unity.json` → `VdfFighter.Build`，全部 URP Lit）：

  | 种类 | 做法 |
  |---|---|
  | 皮肤（脸、上下身、手、牙） | 底色图乘 Blender 里的色相/饱和度/明度和色调（线性空间，和 Blender 节点同一公式），不透明，光滑度 0.45 |
  | 服装（裙子、鞋、头冠、手套） | 同上，加 ARM 图：R 遮蔽、G 粗糙度、B 金属度 → URP 的金属度图（R 金属、G 遮蔽、A 光滑 = 1 − 粗糙度）；底色图的透明通道裁剪（阈值 0.33）；双面 |
  | 头发 | 发根 / 中段 / 发梢三个颜色沿 FR 图的蓝通道插值成底色，ODI 图的红通道裁剪，双面；mip 保持覆盖率，远看不变稀 |
  | 眉毛、睫毛 | 常数颜色，ODI 红通道裁剪 |
  | 眼球 | 虹膜是程序化的（build_blend 的 `build_eye`），用 Cycles 烘成一张 1024 的图 |
  | 眼影壳、泪膜、假反光 | 半透明，不投影 |

  法线图是 UE 的 DirectX 格式，`vdf_textures.py` 把绿通道翻过来再给 Unity。

## 2. 高跟鞋

`RoeHumanoid.StandOnHeels`（只在 `VdfFighter` 里用）：

- 在建人形骨架用的那份 T 姿势副本上，量每只鞋的鞋底：
  - 鞋跟：从脚踝往后到脚趾骨 40% 处之前的最低点；
  - 前掌：脚趾骨前 40% 范围内的最低点。
- 两点高差和水平距离给出要压的角度：左 32.4°、右 32.7°（鞋跟低 5.3 厘米，相距 8.4 厘米）。
- 脚绕脚踝往下压这个角度，脚趾骨转回原来的朝向（平的）。
- 小于 4° 不动（平底鞋、光脚）。

## 3. 衣服和头发：游戏自己的 KawaiiPhysics

### 怎么查到的

- 后台调研读了游戏包的头信息和数据：
  - 裙子、头冠、手套这几个骨骼网格各自挂一个后处理动画蓝图（`ABP_PCF_005_*_master`），头发挂 `ABP_Fiona_Hair01`；
  - 蓝图里只用 `AnimNode_KawaiiPhysics`：没有 Chaos 布料，没有 RigidBody、AnimDynamics；
  - 碰撞体在 `KawaiiPhysicsLimitsDataAsset` 里，全是胶囊；物理资产存在，但这些节点都关了世界碰撞，没用到。
- 游戏包是"无版本属性"格式，没有映射文件（.usmap），手工按字节解出来。
  - 每个节点真正用的参数不是节点上存的默认值，而是蓝图字节码里一个"拼结构体"的常量，接到节点的 PhysicsSettings 上。
- 读数据的脚本在 `tools/vdf_research/`（从后往前：`zen_dump.py` 解出包 → `run_decode.py` 用 `kawaii_decode.py` 解字段 → `kawaii_params.json`；
  `anim_inventory.py` 列动作清单）。只读游戏文件；AES 密钥只从密钥文件读进变量，不打印、不落盘。

### 参数（游戏单位：厘米、度；WorldDamping 全是 0.8 / 0.8）

| 部位 | 节点 | 阻尼 / 刚度 / 半径 / 角度限制 | 重力 | 碰撞体 |
|---|---|---|---|---|
| 裙子 | 14 条链各一个节点（`Outfit005_skirt_{a..g}_01_{l,r}`），链和链之间不连 | 0.3 / 0.2 / 4.0 / 无；刚度和半径从链根 ×0.5 到末端 ×1.0 | −980（9.8 m/s²） | 大腿 R10 L30、大腿后侧 R9 L10、手 R2 L10 |
| 领口羽毛 | `Outfit005_spine05_feather_root` | 0.5 / 0.3 / 3.0；半径同上曲线 | 无 | 锁骨、spine_04/05、两边胸、上臂、脖子，共 9 个 |
| 头冠羽毛和耳环 | `Outfit005_head_root` | 0.5 / 0.3 / 3.0 | 无 | 头 R6.8 L4.6 |
| 上臂羽毛 | `Outfit005_hand_feather_root_l/r` | 0.5 / 0.2 / 2.0 | 无 | 上臂 R3.5 L20 |
| 头发 | 11 个节点：b、c、d 三组各一个（各排除几缕），排除的几缕和 a 组 4 缕各一个 | 刚度都是 0.2；阻尼 0.3（a、d）或 0.5（b、c）；半径 2.0（a、b）、1.5（c）、0（d） | 无 | 头骨两个胶囊 R6–10 L7 |
| 胸 | `breast_physics_01_l/r`（整棵子树一起模拟） | 0.1 / 0.05 / 1.5 / 70°，角度限制从根 ×1 到末端 ×0.5 | 无 | 上臂、前臂 R4 L22，手 R5 L10 |

头发、羽毛、胸都没有重力：它们是往动画姿势拉的弹簧加惯性，只有裙子会往下坠。游戏还开着风（风力 ×0.5），格斗场景里没有风源，这里不做。

### 算法（`RoeKawaiiPhysics`，照 KawaiiPhysics v1.14 的 `AnimNode_KawaiiPhysics.cpp`）

每一步、在角色自己的坐标系里（UE 的"组件空间"）：

1. 节点的根骨骼跟动画走，不模拟；根下面的每根骨骼是一个质点。
2. 速度 = （这一步的位置 − 上一步的位置）÷ 上一步的时长，乘 （1 − 阻尼）。
3. 加上角色自己这一步的平移和转身的 （1 − WorldDamping）：0.8 表示角色移动的 80% 不会甩到布上，只有 20% 留作惯性。
   一步里移动超过 3 米、转身超过 10° 当瞬移，不算（插件默认值）。
4. 重力 ½·g·dt²。
5. 往动画里它相对父骨骼的位置拉：拉的比例 1 −（1 − 刚度）^(60·dt)。
6. 推出碰撞胶囊（骨骼的半径加胶囊的半径）。
7. 角度限制：和动画方向的夹角超过限制就转回来（只有胸用）。
8. 恢复到父骨骼的距离（动画里的骨长）。
9. 写回：只有一个子骨骼的骨骼转过去对准子骨骼的模拟位置；有多个子骨骼的保持动画的朝向，只移动位置。

参数沿链的曲线按"离根的骨长 ÷ 这个节点最长的那条"取值。

### 坐标换算

- 游戏的碰撞体位置写在 UE 的骨骼局部坐标里。
- 经过 PSK 导入（Y 镜像）、Blender 的 FBX 导出（坐标轴旋转只在根上）、Unity 导入（X 镜像），一个骨骼局部向量 (x, y, z) 在 Unity 里是 (−x, −y, z)。
- 世界向量（重力）是 (−x, z, y)，再厘米变米。
- `VdfFighter.Build` 建完预制体后逐个检查碰撞体落在身上哪里，日志里有每一个的数字：
  - 大腿胶囊的中心在大腿骨往膝盖 15.0 厘米、偏离骨轴 0.0 厘米，轴和骨骼平行，离皮肤 8.5 厘米（半径 10，比腿粗 1.5 厘米，游戏的裙子碰撞体本来就这样）；
  - 上臂的在往肘部 10.0 厘米、偏 0.0；
  - 手的在往掌心 5–6 厘米、偏 3 厘米；
  - 头发的两个在头骨里（离头皮 3.6–5.6 厘米）；
  - 头冠的在头里（离皮肤 6.3 厘米）。
- 以上和这些胶囊在游戏里的意思对得上，说明换算是对的。

## 4. 动作：她游戏里自己的（10-04 夜）

用户："继续"。上一轮最后提的下一步就是这个：她在游戏里的长剑加盾动作。

```powershell
# 1. UE Viewer 把她的 191 段动画导出成 .psa（_work\vdf\anim_umodel，1.5 GB；表情动画不导）
python tools\vdf_anims.py export
python tools\vdf_anims.py list                      # 每段的帧数、长度、根骨位移
# 2. 套到她自己的骨架上，转成人形动作 → Assets\VDF\fio005\anims（默认跳过梯子、攀爬、坡道、跳跃、长舞蹈）
.\tools\unity_batch.ps1 -Method RoeFighter.EditorTools.VdfAnims.Import [-Extra '-roeOnly','Attack0']
# 3. 剑和盾：导出、贴图、清单，再挂到她身上（VdfFighter.Build 也会做）
python tools\vdf_weapons.py --export
.\tools\unity_batch.ps1 -Method RoeFighter.EditorTools.VdfFighter.Weapons
# 4. 她的招式包；全部候选动作的检查图
.\tools\unity_batch.ps1 -Method RoeFighter.EditorTools.RoeMotionPacks.Import -Extra '-roeSpec','tools\motionpacks\vdf_fiona.json'
.\tools\unity_batch.ps1 -Method RoeFighter.EditorTools.RoeMotionPacks.Sheet -Graphics -Extra '-roePack','vdf_candidates','-roeChars','fio005'
python tools\strike_sheet.py out\motion_sheets\vdf_candidates out\fio005_own_moves_sheet.jpg --tile 170
# 5. 前臂扭转骨的对比特写
.\tools\unity_batch.ps1 -Method RoeFighter.EditorTools.VdfAnims.TwistStills -Graphics
```

### 读 .psa

- UE Viewer（spiritovod 的 UE5 版，模型也是它导的）能直接导出 UE 5.3 的 AnimSequence。
  - 60 帧/秒。
  - 骨架 `SK_PCF_BaseBody01_Skeleton` 的 1359 根骨骼都写进去，她的网格有其中 622 根。
  - 不支持通配符，`vdf_anims.py` 一段一段导。
- **数据约定是测出来的，不是猜的。**
  - 关键帧是 UE 局部变换的"Y 镜像再取共轭"（ActorX 的右手约定）。
  - BONENAMES 块里的参考姿势却是 UE 原样的。
  - 导入器拿一段动作里没动的骨骼（Attack01 里 550 根）去比她的绑定姿势，四种读法的中位误差：原样 3.86°、共轭 22.19°、镜像 19.94°、镜像加共轭 **0.00°**，自动选最后一种。
  - 根骨也正好落在原位（0.00°、0.0 mm）。
  - UE Viewer 导出的 PSK（网格、武器）用的是同一个约定。
- **换到 Unity**：
  - 局部旋转 (x, y, z, w) → (−x, y, z, w)；
  - 平移 (x, y, z) → (−x, y, z)·0.01；
  - 也就是 UE 原样的 (−x, −y, z)，和第 3 节 KawaiiPhysics 碰撞体验证过的骨骼空间换算一致；
  - 根骨走 UE 组件空间 (−x, z, y)。
- **每一帧怎么套**：
  - 她有的骨骼都按游戏转；
  - 骨盆和根骨按游戏平移，别的骨骼保持她网格的长度；
  - 再用"不压鞋跟"的那份人形骨架读出肌肉值。她的格斗骨架脚是压过 32° 的（第 2 节），这样游戏里平放的脚套回她身上，也踩在鞋跟上，和别的动作一样。

### 还原度

导入器自检：人形动作放回她身上，和游戏原样比，报告 `_work\vdf\fio005_anims_import.txt`。

| | 平均 | 最差 |
|---|---|---|
| 站架（Battle_Idle）的手 / 头 | 0.2 / 0.4 cm | 0.3 / 0.5 cm |
| 87 段的胸口（spine_05） | 1.3 cm（`RoeUeRig` 前 3.2） | 7 cm（前 12） |
| 87 段的头 | 1.3 cm | 7 cm |
| 87 段的手 | 2.3–2.5 cm | 10 cm |
| 脚踝 | 0.02 cm | 0.4 cm |

- **手的偏差出在肩关节。**
  - 上臂方向平均差 3°，锁骨完全对得上。
  - Unity 人形的肘只有一个铰链轴，游戏的姿势不能完全表示，这是 Mecanim 重定向本身的损耗。
  - 肌肉值超出默认范围的没有被截掉：锁骨、上胸都有超过 2 的，回放出来照样对。
- **扭转分配**（Avatar 的 upperArmTwist / lowerArmTwist / upperLegTwist / lowerLegTwist）：
  - MetaHuman 用 **(1, 0, 1, 0)**：上臂、大腿保留自己的滚转，前臂、小腿的滚转给手、脚。
  - 用 `-roeTwist` 对比实测：

    | 设置 | 前臂旋转误差 | 上臂旋转误差 |
    |---|---|---|
    | (1, 0, 1, 0) | 5.8° | 5.6° |
    | 原来的 (1, 1, 1, 1) | 32°，最差 179° | 5.6° |
    | 上臂设 0 | — | 28° |

  - `RoeHumanoid.BuildAvatar` 遇到 UE 骨架默认用它，ROE 角色不变。
- 大腿、小腿各有 8° 的固定滚转差（方向完全对，膝盖位置对），原因没查。

### RoeUeRig：人形骨架补不回来的部分

- **脊柱、脖子**：人形骨架只有 3 节脊柱、1 节脖子，MetaHuman 是 5 节、2 节。
  - 原来 spine_02、spine_04、neck_02 的弯曲全并到上面那节里，胸口平均差 3.2 cm。
  - 现在每对按比例分回去：spine_02 0.8、spine_04 0.3、neck_02 0。
  - 比例是在她 90 段动作上按胸口和头的位置拟合的（`tools/vdf_research/spine_share_sim.py`）：模拟 3.22 → 1.33 cm，Unity 里实测 1.26 cm。
  - 按角度拟合的比例（0.2 / 0.75 / 0.3）位置反而差，没用。
- **扭转骨（16 根）**：按骨骼在肢体上的位置分。
  - 前臂、小腿：离肘（膝）1/3 处的转手（脚）滚转的 1/3，2/3 处的转 2/3。
  - 上臂、大腿：反过来，抵消肢体自己的滚转，1/3 处的转 −2/3。
  - 她剑手的第一刀里手腕翻了 160°，原来前臂皮肤拧成麻花。
  - 上臂那几根和游戏一致（游戏 −0.71，规则 −0.67）。
  - 前臂那几根和游戏动作里烘的值差得多（平均 30°）。游戏运行时还有 `ABP_PCF_Corrective`、`Rig_proc_ControlRig` 重新摆这些骨骼，烘进动作的值不一定是游戏里看到的样子。对比特写见 `VdfAnims.TwistStills`。
- **运行顺序**：
  - FighterRig 每帧评估动画前把中间节复位，评估后分回弯曲；
  - 脚的 IK 之后驱动扭转骨；
  - 编辑器里的截图、量招（`RoeCapture.Pose`）也按这个顺序做。

### 剑和盾

- **模型**：游戏包 `Character/Player/Fiona/Weapon/` 下有：
  - `SK_Longsword01`：长 1.2 m，原点在护手；
  - `SK_Shield01`：48 × 47 cm；
  - 两件都是一根骨骼的刚体网格，带底色、法线、ARM 三张图。底色是 BC6H，UE Viewer 写成 .hdr，`vdf_weapons.py` 自己读。
  - 同目录的 `longsword`、`shield` 是老版 Vindictus（Source 引擎）的模型，骨骼叫 `ValveBiped_Anim_Attachment_RH`，没用。
- **挂点**：她的连衣裙网格里本来就有这两根骨骼，和游戏一样直接挂上去，不加偏移。
  - `weapon_r`：右手下，离手腕 6.7 cm，在手心。
  - `shield_l`：左前臂下，在前臂 60% 处的外侧。
- **剑和盾跟着游戏的动作走**（10-05，用户"继续"，上一轮留下的第三件事）：
  - 人形动作只有肌肉曲线，管不到 `weapon_r`、`weapon_l`、`shield_l` 这几根骨骼，上一版剑和盾一直停在绑定姿势。
  - 量了 184 段 .psa：`shield_l` 在 170 段里不在绑定姿势。几乎所有动作里都固定偏 22.9°，举盾反击 73°，重装防御架势 81°，胜利欢呼 121°，还会前后移动最多 18 cm。
    `weapon_r` 只在喝药、欢呼、舞蹈这些动作里动，胜利欢呼里转了 95°（反手握剑）。
  - 现在 `VdfAnims.Import` 转每段动作时，把这三根骨骼每帧的局部旋转和位置也写进动作，作为普通的骨骼曲线（`VdfAnims.PropBones`，`-roeProps` 可以换别的骨骼，`-` 是不写）。
  - 战斗里这些曲线跟着动作层叠加；换成别的动作包时，那些动作里没有这几根骨骼的曲线，就沿用站架层的值（和 ROE 角色的头发、武器一样）。
  - 放回她身上和游戏原样比，盾的朝向误差平均 26.1° → 5.5°（剩下的就是左前臂本身的误差，同样 5.5°），最差 127° → 17°；剑平均 3.3° → 0°，最差 95° → 0°。
  - 招式量出来和以前完全一样（剑尖速度、有效时间不变）：四个攻击里剑本来就不动。
  - 前后对比 `out\fio005_props.jpg`（`VdfAnims.PropStills -roeTag before/after` 拍，`tools\vdf_prop_sheet.py` 拼）：战斗站架、举盾反击、重装防御架势、盾后突刺、胜利欢呼，左前方和正面。
- **材质**：`tools/vdf_weapons.py` 按游戏的材质参数（父材质 M_Mob_Base）做：
  - 底色乘饱和度、亮度、对比度；
  - 粗糙度按 Roughness Min / Max 映射；
  - ARM 换成 URP 的金属度图；
  - 法线翻绿。
- **网格**：`VdfFighter.AttachWeapons` 直接读 .psk 建网格。
  - 点换成 (−x, y, z)·0.01；
  - 三角形的绕序按法线方向定：剑 3320/3320、盾 9954/9954 个三角形都和法线一致。
- **命中用剑身**：剑上挂 `RoeBlade`（护手到剑尖）。
  - 她用剑手出的招，判定点是剑身上离对手最近的那一点（`Fighter.ActiveHit`）。
  - 量招时用剑尖量出手距离（`RoeMotionPacks.Measure`）。
  - 剑尖速度 ≥ 5 m/s 的那段算有效时间。原来的"最远处 85%"对转身斩只有 0.02 秒。
  - 技能的命中时机按剑尖相对身体的速度找（≥ 20 m/s，`DoaFighter.DetectHits`）。原来按手脚速度，蓄力也被当成出招：冲刺突刺被认成 0.05 秒打中，其实 0.53 秒才刺中。

### 招式

定义在 `tools/vdf/fio005.json` 和 `tools/motionpacks/vdf_fiona.json`。候选全部在她身上拍过检查图（`out\fio005_own_moves_sheet.jpg`，候选包 `vdf_candidates`）再挑。

| 用途 | 片段 | 说明 |
|---|---|---|
| 站架 / 走 / 退 / 跑 | Battle_Idle / Battle_Walk_Loop / 同一段倒放 / Battle_Run_Loop | 她自己的招式包带这四个，换掉 F3 包里的 |
| A | Attack_Strong03 的前 1.0 秒，×1.2 | 盾后突刺 |
| B | Guard_Counter，×1.1 | 举盾后的反击斩 |
| C | Attack01 的前 1.7 秒 | 转身大横斩 |
| D | Attack_Strong04 的前 2.0 秒，能击倒 | 踢一脚再斩 |
| 受击 | Damage_Light_Front | 被打得退一步 |
| 击倒 → 躺地 → 起身 | Damage_Strong_Front_Begin / During / End | 游戏里本来就连着的三段：被打飞、仰面躺着、爬起来 |
| 开场 | Rest2battle_Idle | 从休息姿势拔剑摆出战斗架势 |
| 胜利（循环） | Test_Emo_Cheering_Evy | 举剑欢呼 |
| 技能 1 / 技能 2 / 超必杀 | ActiveSkill01 / ActiveSkill03 / Attack_Finish_Action | 冲刺突刺 / 冲刺连斩 / 多段终结技（中间带一脚） |

- **攻击截短**：游戏的攻击一段 2–3 秒，后面 1–2 秒是慢慢收回待机，攻击只截到挥完（招式包新加的 `from` / `to`）。
- **位移**：游戏的根骨带着她走，攻击前冲 1–2 米，重受击飞 6 米。
  - 普通攻击：位移留在片段里，招式包按匀速扣掉，由格斗逻辑带着她走，世界里的轨迹和游戏一样。
  - 受击、倒地、技能这些"游戏动作"：用导入器做的原地版（`anims/inplace/`，根骨定在起点）。定义里引用了哪些，导入器就做哪些。
- **连招中段**：Attack02–04 和各个 Strong 攻击是连招的中间段，起手不是站架，靠 0.06 秒的混合过渡。
- **UFE 2 的版本**：上一轮用 UFE 2 演示角色做的那套游戏动作，要用可以从 git 历史（8670be9）里的 `tools/vdf/fio005.json` 拿回来。
- **电脑对电脑 120 秒**（她对 a08，`out/fight_cpu_match_fio005_own.mp4`）：
  - 第一局她用超必杀 KO a08；第二局 a08 赢，1:1。
  - 前一遍录像里她被击倒、躺地、起身完整走了一次（起身这条路径第一次在画面里出现）。
  - 后半段两人常被打到场边，镜头跑到栅栏外面被挡住。10-05 修好了，同一场重录的 `out/fight_cpu_match_fio005_cam.mp4`，
    新旧镜头并排 `out/fight_camera_compare.mp4`（[`fight-camera.md`](fight-camera.md)）。
- 前臂扭转骨的对比特写 `out/fio005_twist.jpg`：左边不驱动、右边按位置分滚转，第一刀的 0.18、0.30、0.45 秒。

## 5. 已知问题和下一步

- **肘、膝的修正骨还没驱动。**
  - 扭转骨现在由 `RoeUeRig` 驱动了。
  - `*_correctiveRoot`、`lowerarm_in/out/fwd/bck`、手指的 `*_bulge` 这类修正骨（125 根）在她的动作文件里完全没有关键帧，
    游戏是运行时摆的。`ABP_PCF_Corrective`（以前解过）里只有两个胸的 KawaiiPhysics 节点，所以摆修正骨的是
    `BaseBody_PCF/Model/Rig_proc_ControlRig`（一个 ControlRig，逻辑编译成 RigVM 字节码）。包里没有姿势资产（PoseAsset）。
  - 要照游戏做，得先解这个 ControlRig 的字节码；这里还是跟着父骨骼走，肘、膝大弯时皮肤是普通蒙皮的效果。
- **盾相对前臂的转动**：10-05 已经跟上（第 4 节"剑和盾"）。
- **没用的部分**：58 段表情动画、音效、特效都没用。
- **手臂的残差**：人形重定向的损耗，平均 2–3 cm，最差 10 cm。
- **镜头被场边的栅栏挡住**：10-05 已经解决，镜头只站在场地的空处（[`fight-camera.md`](fight-camera.md)）。
  - 她的招式每下前冲 1–2.5 米，对局容易漂到场边；上一轮用 UFE 招式时对局一直在场地中间，所以是这一轮才出现的。
- **风没做**：游戏的裙子、头发、羽毛开着风（×0.5），格斗场景里没有风源。
- **胸用的是礼服自己蓝图里的那两个节点**（阻尼 0.1）。基础身体的修正蓝图里还有一组（阻尼 0.5、限制 10.8°），PCF_005 没有基础身体网格，没用。
- **游戏的参数下，裙子 14 条链互不相连、没有角度限制。** 高踢、转身时荷叶边会整片掀起来，0.2 秒左右落回。
  这是游戏本来的调法；要更收敛，F4 换成骨骼布料（链连成一片、有最大距离）。
- 脸是 MetaHuman 的 620 根表情骨骼，保持中性表情，没做表情。
