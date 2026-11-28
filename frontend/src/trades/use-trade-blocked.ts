import type { QueryClient } from '@tanstack/react-query';
import type { TradeBlockDTO } from '../data/sdk/model';
import { getTradeBlockedQueryKey, useTradeBlocked } from '../data/sdk/trade/trade.gen';

/**
 * Why a storage pokémon cannot be traded (duplicate or illegal), or undefined.
 * One request per box, shared by every pokémon of the box.
 */
export const useTradeBlock = (boxId: string | undefined, pkmVariantId: string | undefined): TradeBlockDTO | undefined => {
    const query = useTradeBlocked(boxId ?? '', {
        query: {
            enabled: !!boxId && !!pkmVariantId,
            staleTime: 30_000,
            select: response => response.data.find(block => block.pkmVariantId === pkmVariantId),
        },
    });
    return query.data;
};

/** Blocked lists depend on the whole storage and the saves: refetched after any change. */
export const invalidateTradeBlocked = (client: QueryClient) => client.invalidateQueries({
    predicate: query => typeof query.queryKey[ 0 ] === 'string' && query.queryKey[ 0 ].startsWith(getTradeBlockedQueryKey('')[ 0 ]),
});
