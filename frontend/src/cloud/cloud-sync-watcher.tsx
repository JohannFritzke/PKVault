import { useQueryClient } from '@tanstack/react-query';
import React from 'react';
import { useCloudStatus } from '../data/sdk/cloud/cloud.gen';

const POLL_INTERVAL = 30_000;

/**
 * Keeps the automatic cloud sync running while the app is open (the backend syncs when its status is asked),
 * and reloads the app when data from another computer replaced local data.
 * Pokémon received by trade are stored by the backend: the cached data is refreshed.
 */
export const CloudSyncWatcher: React.FC = () => {
    const statusQuery = useCloudStatus({
        query: {
            refetchInterval: POLL_INTERVAL,
            refetchIntervalInBackground: true,
        },
    });

    const queryClient = useQueryClient();
    const tradeRevision = statusQuery.data?.data.tradeClaimRevision;
    const lastTradeRevision = React.useRef<number | undefined>(undefined);

    React.useEffect(() => {
        if (tradeRevision === undefined)
            return;

        if (lastTradeRevision.current !== undefined && tradeRevision !== lastTradeRevision.current) {
            void queryClient.invalidateQueries();
        }
        lastTradeRevision.current = tradeRevision;
    }, [ tradeRevision, queryClient ]);

    const revision = statusQuery.data?.data.syncRevision;
    const firstRevision = React.useRef<number | undefined>(undefined);

    React.useEffect(() => {
        if (revision === undefined)
            return;

        if (firstRevision.current === undefined) {
            firstRevision.current = revision;
            return;
        }

        if (revision !== firstRevision.current) {
            window.location.reload();
        }
    }, [ revision ]);

    return null;
};
