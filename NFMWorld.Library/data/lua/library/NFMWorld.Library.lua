---@class BaseAi
---@field runAi fun(self: BaseAi)

BaseAi = {}


---@class BackendGameObject : NFMWorldLibrary.ITransform
---@field children { [integer]: BackendGameObject }
---@field parent BackendGameObject|nil
---@field position fixed64vector3
---@field rotation f64euler

BackendGameObject = {}


---@class BackendStage
---@field pieces { [integer]: BackendGameObject }
---@field nodes { [integer]: StageObject }
---@field checkpoints { [integer]: StageObject }
---@field fixHoops { [integer]: StageObject }
---@field nlaps integer
---@field name string
---@field path string
---@field stageLoader StageLoader

BackendStage = {}


---@class WallCollision : BackendGameObject, NFMWorldLibrary.ICollidable, NFMWorldLibrary.ITransform
---@field boxes { [integer]: Rad3dBoxDef }
---@field maxRadius integer

WallCollision = {}


---@class StageObject : BackendGameObject, NFMWorldLibrary.IAiNode, NFMWorldLibrary.ICollidable, NFMWorldLibrary.ITransform
---@field originalPlacement PiecePlacement
---@field rad Rad3d
---@field nodeKind AiNodeKind
---@field isSpecial boolean
---@field boxes { [integer]: Rad3dBoxDef }
---@field maxRadius integer
---@field fileName string

StageObject = {}


---@class IGamemodeData

IGamemodeData = {}


---@class PlayerInfo
---@field id string
---@field name string
---@field vehicle string
---@field color Color3

PlayerInfo = {}


---@class AttachmentLineDirection : System.Enum, System.IComparable, System.IConvertible, System.ISpanFormattable, System.IFormattable

AttachmentLineDirection = {}


---@class Rad3d
---@field maxRadius integer
---@field colors { [integer]: Color3 }
---@field stats CarStats
---@field wheels { [integer]: Rad3dWheelDef }
---@field rims Rad3dRimsDef|nil
---@field boxes { [integer]: Rad3dBoxDef }
---@field polys { [integer]: NFMWorldLibrary.Rad.Rad3dPoly }
---@field castsShadow boolean
---@field atp { [integer]: LuaVector2 }
---@field fileName string
---@field atLines { [integer]: Rad3dAttachmentLine }|nil

Rad3d = {}


---@class LuaVector2 : System.IEquatable_LuaVector2
---@field x number
---@field y number

LuaVector2 = {}


---@class Rad3dAttachmentLine : System.IEquatable_Rad3dAttachmentLine
---@field direction AttachmentLineDirection
---@field offset fixed64

Rad3dAttachmentLine = {}


---@class Rad3dBoxDef : System.IEquatable_Rad3dBoxDef
---@field xy integer
---@field zy integer
---@field radius fixed64vector3
---@field translation fixed64vector3
---@field surfaceType SurfaceType
---@field damage integer
---@field notWall boolean
---@field color Color3
---@field tractionMultiplier fixed64|nil

Rad3dBoxDef = {}


---@class Rad3dRimsDef : System.IEquatable_Rad3dRimsDef
---@field color Color3
---@field size number
---@field depth number

Rad3dRimsDef = {}


---@class Rad3dWheelDef : System.IEquatable_Rad3dWheelDef
---@field position fixed64vector3
---@field rotates integer
---@field width fixed64
---@field height fixed64

Rad3dWheelDef = {}


---@class SurfaceType : System.Enum, System.IComparable, System.IConvertible, System.ISpanFormattable, System.IFormattable

SurfaceType = {}


---@class CarPhysics
---@field halted boolean
---@field btab boolean
---@field capcnt integer
---@field capsized boolean
---@field caught { [integer]: boolean }
---@field stat CarStats
---@field cn integer
---@field cntdest integer
---@field cntouch integer
---@field collidingWithClientPlayer boolean
---@field crank NFMWorldLibrary.Array2D_int
---@field lcrank NFMWorldLibrary.Array2D_int
---@field cxz fixed64
---@field staticCameraXz fixed64
---@field dcnt integer
---@field dcomp fixed64
---@field lcomp fixed64
---@field wasted boolean
---@field dominate NFMWorldLibrary.Util.UnlimitedArray_bool
---@field drag fixed64
---@field fixes integer
---@field forca fixed64
---@field ftab boolean
---@field turnXz fixed64
---@field gtouch boolean
---@field hitmag integer
---@field im integer
---@field lastcolido integer
---@field loop integer
---@field lxz fixed64
---@field mtouch boolean
---@field mxz fixed64
---@field numRoofDamage integer
---@field newcar boolean
---@field newedcar integer
---@field nmlt integer
---@field nofocus boolean
---@field outshakedam integer
---@field pd boolean
---@field pl boolean
---@field pmlt integer
---@field point integer
---@field power fixed64
---@field powerup fixed64
---@field pr boolean
---@field pu boolean
---@field pushed boolean
---@field pxy fixed64
---@field pzy fixed64
---@field rcomp fixed64
---@field rtab boolean
---@field scx { [integer]: fixed64 }
---@field scy { [integer]: fixed64 }
---@field scz { [integer]: fixed64 }
---@field shakedam integer
---@field skid integer
---@field speed fixed64
---@field roofDamage integer
---@field surfCount integer
---@field surfing boolean
---@field tilt fixed64
---@field totalStuntXy fixed64
---@field totalStuntXz fixed64
---@field totalStuntZy fixed64
---@field tcnt integer
---@field txz fixed64
---@field ucomp fixed64
---@field wtouch boolean
---@field xtpower integer

CarPhysics = {}


---@class CarStats : System.IEquatable_CarStats
---@field swits Int3
---@field acelf fixed64vector3
---@field handb integer
---@field airs fixed64
---@field airc integer
---@field grip fixed64
---@field bounce fixed64
---@field simag fixed64
---@field moment fixed64
---@field comprad fixed64
---@field push fixed64
---@field revpush fixed64
---@field lift integer
---@field revlift integer
---@field powerloss integer
---@field flipy integer
---@field msquash integer
---@field clrad integer
---@field dammult fixed64
---@field maxmag integer
---@field dishandle fixed64
---@field outdam fixed64
---@field name string
---@field enginsignature integer
---@field turnradius integer
---@field roadgrip fixed64|nil
---@field offroadgrip fixed64|nil
---@field offtrackgrip fixed64|nil
---@field turn fixed64

CarStats = {}


---@class Int3 : System.IEquatable_Int3
---@field x integer
---@field y integer
---@field z integer

Int3 = {}


---@class Control
---@field arrace boolean
---@field chatup integer
---@field down boolean
---@field enter boolean
---@field exit boolean
---@field handb boolean
---@field multion integer
---@field mutem boolean
---@field mutes boolean
---@field radar boolean
---@field right boolean
---@field up boolean
---@field left boolean
---@field lookback integer
---@field wall integer
---@field zyinv boolean

Control = {}


---@class AiNodeKind : System.Enum, System.IComparable, System.IConvertible, System.ISpanFormattable, System.IFormattable

AiNodeKind = {}


---@class PiecePlacement : System.IEquatable_PiecePlacement
---@field type PiecePlacementType
---@field object Rad3d
---@field position fixed64vector3
---@field rotation f64euler
---@field nodeKind AiNodeKind|nil
---@field isSpecial boolean
---@field isWall boolean

PiecePlacement = {}


---@class PiecePlacementType : System.Enum, System.IComparable, System.IConvertible, System.ISpanFormattable, System.IFormattable

PiecePlacementType = {}


---@class StageWall : System.IEquatable_StageWall
---@field direction WallDirection
---@field count integer
---@field position integer
---@field offset integer

StageWall = {}


---@class WallDirection : System.Enum, System.IComparable, System.IConvertible, System.ISpanFormattable, System.IFormattable

WallDirection = {}


---@class HierarchyGroup : System.IEquatable_HierarchyGroup
---@field name string
---@field pieces NFMWorldLibrary.Util.UnlimitedArray_PiecePlacement
---@field coordinateKeys NFMWorldLibrary.Util.UnlimitedArray_string

HierarchyGroup = {}


---@class EnvironmentInstruction : System.IEquatable_EnvironmentInstruction

EnvironmentInstruction = {}


---@class SnapInstruction : EnvironmentInstruction, System.IEquatable_EnvironmentInstruction, System.IEquatable_SnapInstruction
---@field color Color3

SnapInstruction = {}


---@class SkyInstruction : EnvironmentInstruction, System.IEquatable_EnvironmentInstruction, System.IEquatable_SkyInstruction
---@field color Color3

SkyInstruction = {}


---@class FogInstruction : EnvironmentInstruction, System.IEquatable_EnvironmentInstruction, System.IEquatable_FogInstruction
---@field color Color3

FogInstruction = {}


---@class CloudsInstruction : EnvironmentInstruction, System.IEquatable_EnvironmentInstruction, System.IEquatable_CloudsInstruction
---@field clouds { [integer]: integer }

CloudsInstruction = {}


---@class GroundInstruction : EnvironmentInstruction, System.IEquatable_EnvironmentInstruction, System.IEquatable_GroundInstruction
---@field color Color3

GroundInstruction = {}


---@class TextureInstruction : EnvironmentInstruction, System.IEquatable_EnvironmentInstruction, System.IEquatable_TextureInstruction
---@field texture { [integer]: integer }

TextureInstruction = {}


---@class PolysInstruction : EnvironmentInstruction, System.IEquatable_EnvironmentInstruction, System.IEquatable_PolysInstruction
---@field color Color3

PolysInstruction = {}


---@class LuaVector3 : System.IEquatable_LuaVector3
---@field x number
---@field y number
---@field z number

LuaVector3 = {}


---@class StageLoader
---@field path string
---@field nlaps integer
---@field musicPath string
---@field remasteredMusicPath string
---@field musicFreqMul number
---@field musicTempoMul number
---@field name string
---@field indexOffset integer
---@field sx integer
---@field sz integer
---@field ncx integer
---@field ncz integer
---@field cloudCoverage number|nil
---@field fogDensity integer|nil
---@field fadeFrom integer|nil
---@field lightsOn boolean
---@field drawMountains boolean
---@field mountainSeed integer|nil
---@field mountainCoverage number|nil
---@field lightDirection LuaVector3|nil
---@field pieces { [integer]: PiecePlacement }
---@field walls { [integer]: Rad3dBoxDef }
---@field maxr integer
---@field maxl integer
---@field maxt integer
---@field maxb integer
---@field environmentInstructions { [integer]: EnvironmentInstruction }
---@field drawPolys boolean
---@field drawClouds boolean

StageLoader = {}


---@class DeterministicRandom
---@field next fun(self: DeterministicRandom): integer
---@field nextBetween fun(self: DeterministicRandom, min: integer, max: integer): integer
---@field nextf64 fun(self: DeterministicRandom): fixed64

DeterministicRandom = {}


---@class Color3 : System.IEquatable_Color3
---@field r integer
---@field g integer
---@field b integer

Color3 = {}


---@class FrameTrace

FrameTrace = {}


---@param message string
function FrameTrace.addMessage(message) end

---@class ClientSidePlayer
---@field parameters ClientSidePlayerParameters
---@field index integer
---@field car BackendCar|nil
---@field bot BaseAi|nil
---@field isFake boolean

ClientSidePlayer = {}


---@class ClientSidePlayerParameters
---@field playerName string
---@field carName string
---@field color Color3
---@field isBot boolean
---@field isClientPlayer boolean

ClientSidePlayerParameters = {}


---@class PhysicsController
---@field gameTick fun(self: PhysicsController)

PhysicsController = {}


---@class LuaClientContext
---@field resetCheckpointGlow fun(self: LuaClientContext)
---@field updateCheckpointGlow fun(self: LuaClientContext, currentCheckpoint: integer, isFinish: boolean)
---@field getClientCarCallbacks fun(self: LuaClientContext, car: BackendCar): LuaClientCarContext

LuaClientContext = {}


---@class LuaClientCarContext
---@field castsShadow boolean
---@field getsShadowed boolean|nil
---@field alphaOverride number|nil
---@field glow boolean|nil
---@field finish boolean|nil

LuaClientCarContext = {}


---@class GamemodeContext
---@field stage BackendStage
---@field players { [integer]: ClientSidePlayer }
---@field clientPlayer ClientSidePlayer
---@field hudState HudStateData
---@field physics PhysicsController
---@field timeTrial TimeTrial
---@field config table|nil
---@field client LuaClientContext
---@field countdownInterval integer
---@field createCar fun(self: GamemodeContext, playerIndex: integer, x: fixed64, z: fixed64): BackendCar
---@field calculatePositions fun(self: GamemodeContext)
---@field handleCheckPoint fun(self: GamemodeContext, car: BackendCar): boolean
---@field handleFixHoops fun(self: GamemodeContext, car: BackendCar): boolean
---@field clientReset fun(self: GamemodeContext)
---@field sendEvent fun(self: GamemodeContext, type: string, payload: table)
---@field updateHudAndSounds fun(self: GamemodeContext, car: BackendCar)
---@field removeFakePlayers fun(self: GamemodeContext)
---@field clonePlayer fun(self: GamemodeContext, basedOnPlayer: ClientSidePlayer): ClientSidePlayer

GamemodeContext = {}


---@class ServerGamemodeContext
---@field currentStage BackendStage
---@field playerIds { [integer]: string }
---@field playerInfos { [integer]: PlayerInfo }
---@field config table|nil
---@field countdownInterval integer
---@field getPlayerPosition fun(self: ServerGamemodeContext, playerId: string): fixed64vector3|nil
---@field broadcastEvent fun(self: ServerGamemodeContext, type: string, payload: table)
---@field finishRace fun(self: ServerGamemodeContext, standings: RaceStandings)

ServerGamemodeContext = {}


---@class TimeTrial
---@field hasGhost boolean
---@field begin fun(self: TimeTrial, car: BackendCar)
---@field applyGhost fun(self: TimeTrial, ghostCar: BackendCar, tick: integer)
---@field getSplitDiff fun(self: TimeTrial, splitIndex: integer): number|nil
---@field getLastSplitDiff fun(self: TimeTrial): number|nil
---@field getLapDiff fun(self: TimeTrial, lapIndex: integer): number|nil
---@field recordSplit fun(self: TimeTrial, splitTime: number)
---@field getLapTime fun(self: TimeTrial, lapIndex: integer): number|nil
---@field record fun(self: TimeTrial, car: BackendCar)
---@field save fun(self: TimeTrial)

TimeTrial = {}


---@class HudStateData
---@field speed number
---@field power number
---@field damage number
---@field lap integer
---@field totalLaps integer
---@field lapTime integer
---@field position integer
---@field totalRacers integer
---@field stateText string
---@field lapDiffMs integer|nil
---@field lastLapDiffMs integer|nil
---@field chkDiffMs integer|nil
---@field lastChkDiffMs integer|nil
---@field countdownTimer integer
---@field stateTextEndsAt number|nil

HudStateData = {}


---@class BackendCar : BackendGameObject, NFMWorldLibrary.ITransform
---@field groundAt integer
---@field maxRadius integer
---@field wheelAngle f64euler
---@field turningWheelAngle f64euler
---@field wheels { [integer]: Rad3dWheelDef }
---@field carPhysics CarPhysics
---@field control Control
---@field currentCheckpoint integer
---@field currentLap integer
---@field totalCheckpoint integer
---@field lastCheckpointNode integer
---@field placement integer
---@field rad Rad3d
---@field stats CarStats
---@field wasted boolean
---@field player ClientSidePlayerParameters
---@field drive fun(self: BackendCar, stage: BackendStage)

BackendCar = {}


---@class AiContext
---@field players { [integer]: ClientSidePlayer }
---@field player ClientSidePlayer
---@field stage BackendStage
---@field config table|nil

AiContext = {}


---@class Stopwatch
---@field isRunning boolean
---@field elapsed number
---@field elapsedMilliseconds integer
---@field elapsedMicroseconds integer
---@field stop fun(self: Stopwatch)
---@field start fun(self: Stopwatch)
---@field restart fun(self: Stopwatch)
---@field reset fun(self: Stopwatch)

Stopwatch = {}


---Creates a new Stopwatch
---@return Stopwatch
function Stopwatch.new() end

---@return Stopwatch
function Stopwatch.startNew() end

