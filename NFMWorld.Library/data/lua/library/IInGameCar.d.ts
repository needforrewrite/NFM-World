declare interface IInGameCar
    extends ICar, ITransform
{
    readonly car_physics: CarPhysics;
    readonly control: Control;
    current_checkpoint: number;
    nlaps: number;
    clear: number;
    last_checkpoint_node: number;
    placement: number;
    readonly wasted: boolean;
    readonly player: PlayerParameters;
    readonly rad: Rad3d;
    readonly stats: CarStats;
    readonly groundAt: number;
    readonly maxRadius: number;
    wheelAngle: fixed64euler;
    turningWheelAngle: fixed64euler;
    readonly wheels: Rad3dWheelDef;
    readonly childTransforms: ITransform;
    position: fixed64vector3;
    rotation: fixed64euler;
    readonly parent: ITransform;
    addDust(wheelidx: number, x: number, y: number, z: number, scx: number, scz: number, simag: number, tilt: number, onRoof: boolean, wheelGround: number): void;
    spark(x: number, y: number, z: number, scx: number, scy: number, scz: number, type: number, wheelGround: number): void;
    damageX(wheelnum: number, amount: fixed64): void;
    damageY(wheelnum: number, amount: fixed64, mtouch: boolean, nbsq: number, squash: number): void;
    damageZ(wheelnum: number, amount: fixed64): void;
    drive(stage: IStage): void;
    collide(otherCar: IInGameCar): void;
    resetPosition(): void;
    fix(): void;
}
