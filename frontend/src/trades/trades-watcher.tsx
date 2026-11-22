import { useQueryClient } from '@tanstack/react-query';
import React from 'react';
import { useTradeStatus } from '../data/sdk/trade/trade.gen';

const POLL_INTERVAL = 5_000;
// received pokémon are being stored by the backend: check more often until they are in the storage
const POLL_INTERVAL_CLAIMING = 2_000;

/**
 * Keeps trades up to date in the whole app: when a friend offers, answers, accepts or declines,
 * or when received pokémon are stored, the cached data (trades, storage) is refreshed.
 */
export const TradesWatcher: React.FC = () => {
    const queryClient = useQueryClient();

    const statusQuery = useTradeStatus({
        query: {
            refetchInterval: query => query.state.data?.data.toClaimCount ? POLL_INTERVAL_CLAIMING : POLL_INTERVAL,
            refetchIntervalInBackground: true,
        },
    });
    const status = statusQuery.data?.data;

    const signature = status?.loggedIn
        ? [
            status.claimRevision,
            status.toClaimCount,
            ...status.trades.map(trade => `${trade.id}:${trade.status}:${trade.countered?.fingerprint ?? ''}`).sort(),
        ].join('|')
        : undefined;

    const lastSignature = React.useRef<string | undefined>(undefined);

    React.useEffect(() => {
        if (signature === undefined)
            return;

        if (lastSignature.current !== undefined && signature !== lastSignature.current) {
            void queryClient.invalidateQueries();
        }
        lastSignature.current = signature;
    }, [ signature, queryClient ]);

    return null;
};
