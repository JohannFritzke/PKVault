import { Alert, Text, Tooltip } from '@mantine/core';
import { useQueryClient } from '@tanstack/react-query';
import { ArrowLeftRightIcon, SendIcon, TriangleAlertIcon } from 'lucide-react';
import React from 'react';
import { usePkmIndex } from '../../../data/hooks/use-pkm-index';
import { TradeStatus, type PkmVariantDTO, type TradeActionResultDTO } from '../../../data/sdk/model';
import { useTradeCounter, useTradeOffer, useTradeStatus } from '../../../data/sdk/trade/trade.gen';
import { tradeErrorKeys } from '../../../trades/trade-util';
import { useTranslate } from '../../../translate/i18n';
import { UIButton } from '../../../ui/form/button/ui-button';
import { UISelect } from '../../../ui/form/select/ui-select';
import { UITextInput } from '../../../ui/form/text-input/ui-text-input';
import { usePopover } from '../../../ui/interaction/focus-controls/components/popover/hooks/use-popover';
import { UIPopoverCard } from '../../../ui/popover/popover-card/ui-popover-card';
import { UIPopover } from '../../../ui/popover/ui-popover';
import { pick } from '../../../util/pick';
import { useSelectCallback } from '../../../util/use-select-callback';
import type { ActionButtonDefaultProps } from './edit-pkm-button';

/**
 * Offers a storage pokémon to a friend, or answers a received offer with it (counter-offer).
 * Only for one storage pokémon, not attached to a save.
 */
export const OfferTradeButton: React.FC<ActionButtonDefaultProps> = ({ pkmIds, saveId }) => {
    const { t } = useTranslate();

    const statusQuery = useTradeStatus({ query: { enabled: saveId === null } });
    const loggedIn = !!statusQuery.data?.data.loggedIn;

    const pkmIndexQuery = usePkmIndex(null,
        useSelectCallback(data => {
            const pkm = data.data.byId[ pkmIds[ 0 ] ?? '' ] as PkmVariantDTO | undefined;
            return pkm && pick(pkm, [ 'id', 'isExternal', 'attachedSaveId', 'isEnabled', 'isEgg', 'canDelete' ]);
        }, [ pkmIds ]),
        { enabled: saveId === null });
    const pkm = pkmIndexQuery.data;

    if (saveId !== null || pkmIds.length !== 1 || !pkm || !loggedIn)
        return null;

    const attached = pkm.attachedSaveId !== undefined && pkm.attachedSaveId !== null;
    const canTrade = !attached && !pkm.isExternal && pkm.isEnabled && !pkm.isEgg && pkm.canDelete;

    return <UIPopover
        position='left'
        nested
        dropdown={canTrade && <OfferTradeForm pkmVariantId={pkm.id} />}
    >
        <Tooltip label={attached ? t('trades.offer.attached') : t('trades.offer.description')} multiline maw={260}>
            <UIButton
                name='offer-trade'
                controlLabel={t('trades.offer')}
                variant='default'
                size='compact-md'
                leftSection={<ArrowLeftRightIcon />}
                disabled={!canTrade}
            >
                {t('trades.offer')}
            </UIButton>
        </Tooltip>
    </UIPopover>;
};

const OfferTradeForm: React.FC<{ pkmVariantId: string }> = ({ pkmVariantId }) => {
    const { t } = useTranslate();
    const popover = usePopover();
    const queryClient = useQueryClient();

    const status = useTradeStatus().data?.data;
    const offerMutation = useTradeOffer();
    const counterMutation = useTradeCounter();

    const answerable = (status?.trades ?? []).filter(trade => !trade.isSender && trade.status === TradeStatus.OPEN);
    const options = [
        ...(status?.friends ?? []).map(friend => ({
            value: `user:${friend.id}`,
            label: t('trades.offer.to-friend', { name: friend.name }),
        })),
        ...answerable.map(trade => ({
            value: `trade:${trade.id}`,
            label: t('trades.offer.answer', { name: trade.fromName }),
        })),
    ];

    const [ target, setTarget ] = React.useState(options[ 0 ]?.value ?? '');
    const [ message, setMessage ] = React.useState('');
    const [ result, setResult ] = React.useState<TradeActionResultDTO>();
    const [ loading, setLoading ] = React.useState(false);

    const submit = async () => {
        const [ kind, id ] = target.split(':') as [ string, string ];
        setLoading(true);
        try {
            const response = kind === 'trade'
                ? await counterMutation.mutateAsync({ tradeId: id, data: { pkmVariantId, message } })
                : await offerMutation.mutateAsync({ data: { pkmVariantId, toUserId: id, message } });
            setResult(response.data);
            if (response.data.success) {
                await queryClient.invalidateQueries();
                popover?.setOpened(false);
            }
        } finally {
            setLoading(false);
        }
    };

    return <UIPopoverCard
        icon={<ArrowLeftRightIcon />}
        title={t('trades.offer')}
        description={t('trades.offer.custody')}
        miw={340}
        maw={400}
        footer={<UIButton
            name='offer-trade-submit'
            controlLabel={t('trades.offer.submit')}
            onClick={submit}
            loading={loading}
            disabled={!target}
            variant='filled'
            w='100%'
            leftSection={<SendIcon />}
        >
            {t('trades.offer.submit')}
        </UIButton>}
    >
        {options.length === 0
            ? <Text c='dimmed' fz='sm'>{t('trades.offer.no-friends')}</Text>
            : <UISelect
                name='offer-trade-target'
                controlLabel={t('trades.offer.target')}
                label={t('trades.offer.target')}
                data={options}
                value={target}
                onChange={value => value && setTarget(value)}
                comboboxProps={{ withinPortal: false }}
            />}

        <UITextInput
            name='offer-trade-message'
            label={t('trades.offer.message')}
            value={message}
            maxLength={300}
            onChange={e => setMessage(e.currentTarget.value)}
        />

        {result && !result.success && result.error !== undefined && <Alert variant='outline' color='yellow' icon={<TriangleAlertIcon />}>
            {t(tradeErrorKeys[ result.error ])} {result.errorDetail}
        </Alert>}
    </UIPopoverCard>;
};
