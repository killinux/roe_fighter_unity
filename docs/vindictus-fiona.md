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
- **动作**：普通攻击、站架、走路跟着动作包走（F3）。受击、倒地、躺地、起身、开场、胜利、三个技能用 UFE 2 演示角色的动作（见 README "动作包 > UFE 2"）。
  - 她在游戏里自己的动作（191 段）还没有转，见最后一节。
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

## 4. 动作

- **普通攻击、站架、走路**：跟着动作包走（F3），和 ROE 角色一样。
  - 站姿 `stance` 取 `bandai1` 的拳击架势，只用来量鞋底高度和不动的骨骼的默认值。
  - 她没有自己的招式包（`pack` 空着），所以 F3 换到 UFE 的包时，她也打 UFE 的招。
- **"游戏动作"用 UFE 2 演示角色的**（`tools/vdf/fio005.json`，候选全部在 Fiona 身上拍过检查图再挑的）：

  | 用途 | 片段 | 来源 |
  |---|---|---|
  | 受击 | 大幅后仰、甩手（0.73 秒） | Robot Kyle `HitStandingHeavy` |
  | 倒地 | 被打得往后倒、摔平（1.53 秒） | Mecanim Bot `get_hit_high_knockdown` |
  | 躺地（循环） | 仰面躺着 | Mecanim Bot `ko_back` |
  | 起身 | 坐起、撑地、站起（1.13 秒） | Robot Kyle `StandUp_Default` |
  | 开场 | 摆姿势（1.87 秒） | Mike `intro`（转成人形的） |
  | 胜利（循环） | 站架晃动（1.47 秒） | Mike `outro` |
  | 技能 1 | 波动拳的推掌（0.93 秒），0.33 秒打中 | Mike `hadouken` |
  | 技能 2 | 升龙拳（0.60 秒），0.10、0.25 秒两下 | Mike `shoryuken` |
  | 超必杀 | 百裂踢（3.29 秒），按手脚速度找出 6 下：1.03、1.20、1.37、1.57、2.07、2.73 秒 | Mecanim Bot `houyoku_sen` |

- **起身**是这次新加的：角色有 `getup` 片段时，倒地后播它站起来再回站架。原来是从躺姿 0.45 秒直接淡回站架，ROE 角色、霞不受影响。
- 电脑对电脑 120 秒（她对 a08，动作包 Robot Kyle）：第一局被 a08 打倒，后两局赢，2:1。录像 `out/fight_cpu_match_fio005.mp4`。
- 技能和霞的一样，按原地播放，技能表里加一段"冲到对手面前"。
  - 没有特效、音效、语音。
  - 波动拳在 UFE 里是发出一个气功弹，这里是近身推掌。

## 5. 已知问题和下一步

- **她在 Vindictus 里自己的动作还没转。**
  - 游戏里有 191 段动作、124 个蒙太奇，清单在 `_work\vdf\research\fiona_animations.txt`（`tools/vdf_research/anim_inventory.py`）：
    - 长剑加盾的 4 段连击；
    - 蓄力重击和三种收尾；
    - 冲刺攻击；
    - 5 个主动技能；
    - 防御、反击；
    - 轻受击 5 个方向，中、重、浮空受击各分开始 / 持续 / 结束；
    - 正面 / 背面死亡；
    - 舞蹈等表情动作。
  - 要转得先能导出 UE5 的压缩动画，像霞那样做成她自己的招式和技能。
- **肘、膝的修正骨没有驱动。**
  - 游戏用 ControlRig 和 PoseDriver 在运行时摆 `*_twistCor_*`、`*_correctiveRoot` 这些骨骼，这里跟着父骨骼走。
  - 肘、膝大弯时皮肤是普通蒙皮的效果。
- **风没做**：游戏的裙子、头发、羽毛开着风（×0.5），格斗场景里没有风源。
- **胸用的是礼服自己蓝图里的那两个节点**（阻尼 0.1）。基础身体的修正蓝图里还有一组（阻尼 0.5、限制 10.8°），PCF_005 没有基础身体网格，没用。
- **游戏的参数下，裙子 14 条链互不相连、没有角度限制。** 高踢、转身时荷叶边会整片掀起来，0.2 秒左右落回。
  这是游戏本来的调法；要更收敛，F4 换成骨骼布料（链连成一片、有最大距离）。
- 脸是 MetaHuman 的 620 根表情骨骼，保持中性表情，没做表情。
