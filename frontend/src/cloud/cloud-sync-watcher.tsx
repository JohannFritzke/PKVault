import React from 'react';
import { useCloudStatus } from '../data/sdk/cloud/cloud.gen';

const POLL_INTERVAL = 30_000;

/**
 * Keeps the automatic cloud sync running while the app is open (the backend syncs when its status is asked),
 * and reloads the app when data from another computer replaced local data.
 */
export const CloudSyncWatcher: React.FC = () => {
    const statusQuery = useCloudStatus({
        query: {
            refetchInterval: POLL_INTERVAL,
            refetchIntervalInBackground: true,
        },
    });

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
