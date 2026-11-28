import { Stack, Text } from '@mantine/core';
import { HistoryIcon } from 'lucide-react';
import type React from 'react';
import { useTradeHistory, useTradeStatus } from '../../data/sdk/trade/trade.gen';
import { useTranslate } from '../../translate/i18n';
import { renderDate } from '../../util/render-date-time';

/** Previous owners of a storage pokémon, from trades between friends. */
export const DetailsHistory: React.FC<{ pkmVariantId?: string }> = ({ pkmVariantId }) => {
    const { t } = useTranslate();

    const loggedIn = !!useTradeStatus({ query: { enabled: !!pkmVariantId } }).data?.data.loggedIn;
    const historyQuery = useTradeHistory(pkmVariantId ?? '', {
        query: { enabled: !!pkmVariantId && loggedIn },
    });
    const history = historyQuery.data?.data ?? [];

    if (!pkmVariantId)
        return <Text c='dimmed' fz='sm'>{t('details.history.save')}</Text>;

    if (history.length === 0)
        return <Text c='dimmed' fz='sm'>{t('details.history.empty')}</Text>;

    return <Stack gap='xs'>
        {history.map((entry, i) => <Text key={i} fz='sm' component='div' style={{ display: 'flex', gap: 8, alignItems: 'center' }}>
            <HistoryIcon size={16} />
            <span style={{ flexGrow: 1 }}>{t('trades.history.entry', { from: entry.fromName, to: entry.toName })}</span>
            <Text component='span' c='dimmed' fz='sm'>{renderDate(new Date(entry.date))}</Text>
        </Text>)}
    </Stack>;
};
