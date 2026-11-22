import { Stack, Text, Tooltip } from '@mantine/core';
import { HistoryIcon } from 'lucide-react';
import type React from 'react';
import { useTradeHistory, useTradeStatus } from '../../../data/sdk/trade/trade.gen';
import { useTranslate } from '../../../translate/i18n';
import { UIActionIcon } from '../../../ui/form/button/ui-action-icon';
import { UIPopoverCard } from '../../../ui/popover/popover-card/ui-popover-card';
import { UIPopover } from '../../../ui/popover/ui-popover';
import { renderDate } from '../../../util/render-date-time';
import type { ActionButtonDefaultProps } from './edit-pkm-button';

/** Previous owners of a storage pokémon, from trades between friends. Hidden when it was never traded. */
export const TradeHistoryButton: React.FC<ActionButtonDefaultProps> = ({ pkmIds, saveId }) => {
    const { t } = useTranslate();

    const pkmVariantId = pkmIds[ 0 ] ?? '';
    const loggedIn = !!useTradeStatus({ query: { enabled: saveId === null } }).data?.data.loggedIn;
    const historyQuery = useTradeHistory(pkmVariantId, {
        query: { enabled: saveId === null && loggedIn && pkmIds.length === 1 },
    });
    const history = historyQuery.data?.data ?? [];

    if (saveId !== null || history.length === 0)
        return null;

    return <UIPopover
        position='left'
        nested
        dropdown={<UIPopoverCard icon={<HistoryIcon />} title={t('trades.history.title')} miw={280}>
            <Stack gap='xs'>
                {history.map((entry, i) => <Text key={i} fz='sm'>
                    {renderDate(new Date(entry.date))} · {t('trades.history.entry', { from: entry.fromName, to: entry.toName })}
                </Text>)}
            </Stack>
        </UIPopoverCard>}
    >
        <Tooltip label={t('trades.history.title')}>
            <UIActionIcon
                name='trade-history'
                controlLabel={t('trades.history.title')}
                variant='default'
                size='md'
            >
                <HistoryIcon />
            </UIActionIcon>
        </Tooltip>
    </UIPopover>;
};
