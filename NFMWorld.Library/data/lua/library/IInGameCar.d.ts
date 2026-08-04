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
    addDust(wheelidx: number, x: number, y: number, z: number, scx: number, scz: number, simag: number, tilt: number, onRoof: boolean, wheelGround: number): void;
    spark(x: number, y: number, z: number, scx: number, scy: number, scz: number, type: number, wheelGround: number): void;
    damageX(wheelnum: number, amount: Fixed64): void;
    damageY(wheelnum: number, amount: Fixed64, mtouch: boolean, nbsq: number, squash: number): void;
    damageZ(wheelnum: number, amount: Fixed64): void;
    drive(stage: IStage): void;
    collide(otherCar: IInGameCar): void;
    resetPosition(): void;
    fix(): void;
}
