import { EntityContext, TradeError } from '../data/sdk/model';

const formatContexts: Record<string, EntityContext> = {
    pk1: EntityContext.Gen1,
    pk2: EntityContext.Gen2,
    sk2: EntityContext.Gen2,
    pk3: EntityContext.Gen3,
    ck3: EntityContext.Gen3,
    xk3: EntityContext.Gen3,
    pk4: EntityContext.Gen4,
    bk4: EntityContext.Gen4,
    rk4: EntityContext.Gen4,
    pk5: EntityContext.Gen5,
    pk6: EntityContext.Gen6,
    pk7: EntityContext.Gen7,
    pb7: EntityContext.Gen7b,
    pk8: EntityContext.Gen8,
    pa8: EntityContext.Gen8a,
    pb8: EntityContext.Gen8b,
    pk9: EntityContext.Gen9,
    pa9: EntityContext.Gen9a,
};

/** Game context of a pokémon file format (pk3, pk9...), for sprites and names. */
export const getFormatContext = (format: string): EntityContext => formatContexts[ format.toLowerCase() ] ?? EntityContext.Gen9;

export const tradeErrorKeys = {
    [ TradeError.NOT_LOGGED_IN ]: 'trades.error.not-logged-in',
    [ TradeError.PENDING_CHANGES ]: 'trades.error.pending-changes',
    [ TradeError.CANNOT_TRADE ]: 'trades.error.cannot-trade',
    [ TradeError.NOT_FOUND ]: 'trades.error.not-found',
    [ TradeError.SERVER ]: 'trades.error.server',
} as const;
