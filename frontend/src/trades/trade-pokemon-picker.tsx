import { Alert, Group, Modal, Stack, Text } from '@mantine/core';
import { useQueryClient } from '@tanstack/react-query';
import { ArrowLeftRightIcon, SendIcon, TriangleAlertIcon } from 'lucide-react';
import React from 'react';
import { usePkmVariantIndex } from '../data/hooks/use-pkm-variant-index';
import { BoxType, Gender, type PkmVariantDTO, type TradeActionResultDTO, type TradeDTO } from '../data/sdk/model';
import { useStorageGetBoxes } from '../data/sdk/storage/storage.gen';
import { useTradeCounter } from '../data/sdk/trade/trade.gen';
import { useStaticData } from '../hooks/use-static-data';
import { SpeciesImg } from '../img/species-img';
import { useTranslate } from '../translate/i18n';
import { UIActionIcon } from '../ui/form/button/ui-action-icon';
import { UIButton } from '../ui/form/button/ui-button';
import { UISelect } from '../ui/form/select/ui-select';
import { UITextInput } from '../ui/form/text-input/ui-text-input';
import { useControls } from '../ui/interaction/controls/use-controls';
import { getBackControl } from '../ui/interaction/focus-controls/common-controls/back-controls';
import { FocusScope } from '../ui/interaction/focus/scope/focus-scope';
import { useFocusScopeContext } from '../ui/interaction/focus/scope/use-focus-scope-context';
import classes from '../ui/trades/ui-trade-picker.module.css';
import { tradeErrorKeys } from './trade-util';

const canTrade = (pkm: PkmVariantDTO) => pkm.isMain
    && !pkm.isExternal
    && (pkm.attachedSaveId === undefined || pkm.attachedSaveId === null)
    && pkm.isEnabled
    && !pkm.isEgg
    && pkm.canDelete;

type TradePokemonPickerProps = {
    trade: TradeDTO;
    opened: boolean;
    onClose: () => void;
};

/**
 * Dialog to answer a received trade: pick one of your storage pokémon, box by box.
 */
export const TradePokemonPicker: React.FC<TradePokemonPickerProps> = ({ trade, opened, onClose }) => {
    const { t } = useTranslate();

    return <Modal
        opened={opened}
        keepMounted={false}
        onClose={onClose}
        size='lg'
        title={<Group gap='sm'>
            <ArrowLeftRightIcon />
            <Text size='lg'>{t('trades.picker.title', { name: trade.fromName })}</Text>
        </Group>}
    >
        <TradePokemonPickerInner trade={trade} onClose={onClose} />
    </Modal>;
};

const TradePokemonPickerInner: React.FC<Omit<TradePokemonPickerProps, 'opened'>> = ({ trade, onClose }) => {
    const { t } = useTranslate();
    const staticData = useStaticData();
    const queryClient = useQueryClient();

    const parentScope = useFocusScopeContext();
    const { controlProps } = useControls(
        'trade-picker',
        true,
        parentScope.parentsIds.length + 1,
        [ getBackControl({ label: t('action.back'), action: onClose }) ],
        { enabled: true },
    );

    const boxes = (useStorageGetBoxes().data?.data ?? [])
        .filter(box => box.type === BoxType.Box)
        .sort((a, b) => (a.bankId ?? '').localeCompare(b.bankId ?? '') || a.order - b.order);
    const indexQuery = usePkmVariantIndex();
    const byBox = indexQuery.data?.data.byBox ?? {};

    const firstBoxWithPkm = boxes.find(box => Object.keys(byBox[ box.idInt ] ?? {}).length > 0) ?? boxes[ 0 ];
    const [ boxId, setBoxId ] = React.useState<number>();
    const currentBoxId = boxId ?? firstBoxWithPkm?.idInt;
    const currentBox = boxes.find(box => box.idInt === currentBoxId);

    const [ selected, setSelected ] = React.useState<PkmVariantDTO>();
    const [ message, setMessage ] = React.useState('');
    const [ result, setResult ] = React.useState<TradeActionResultDTO>();

    const counterMutation = useTradeCounter();

    const submit = async () => {
        if (!selected)
            return;
        const response = await counterMutation.mutateAsync({
            tradeId: trade.id,
            data: { pkmVariantId: selected.id, message },
        });
        setResult(response.data);
        if (response.data.success) {
            await queryClient.invalidateQueries();
            onClose();
        }
    };

    const speciesName = (pkm: PkmVariantDTO) => staticData.species[ pkm.species ]?.forms[ pkm.context ]?.[ pkm.form ]?.name ?? '';

    return <FocusScope id='trade-picker' focusOnMount restoreMode='none'>
        <Stack {...controlProps('back')}>
            <Text c='dimmed' fz='sm'>{t('trades.picker.description')}</Text>

            {boxes.length > 0 && <UISelect
                name='trade-picker-box'
                controlLabel={t('trades.picker.box')}
                label={t('trades.picker.box')}
                data={boxes.map(box => ({ value: box.idInt, label: box.name }))}
                value={currentBoxId ?? 0}
                onChange={value => value !== null && setBoxId(value)}
                comboboxProps={{ withinPortal: false }}
            />}

            {currentBox && <div className={classes.grid}>
                {Array.from({ length: currentBox.slotCount }, (_, slot) => {
                    const pkm = byBox[ currentBox.idInt ]?.[ slot ]?.find(variant => variant.isMain);
                    if (!pkm) {
                        return <div key={slot} className={classes.slot} />;
                    }
                    const tradable = canTrade(pkm);
                    return <div key={slot} className={classes.slot} data-selected={selected?.id === pkm.id}>
                        <UIActionIcon
                            name={`trade-picker-slot-${slot}`}
                            controlLabel={t('action.select')}
                            variant='subtle'
                            size='xl'
                            disabled={!tradable}
                            title={tradable ? speciesName(pkm) : t('trades.picker.not-tradable')}
                            onClick={() => setSelected(pkm)}
                        >
                            <SpeciesImg
                                species={pkm.species}
                                context={pkm.context}
                                form={pkm.form}
                                isFemale={pkm.gender === Gender.Female}
                                isShiny={pkm.isShiny}
                            />
                        </UIActionIcon>
                    </div>;
                })}
            </div>}

            <div className={classes.selected}>
                {selected
                    ? t('trades.picker.selected', { name: selected.nickname || speciesName(selected), level: selected.level })
                    : <Text c='dimmed' fz='sm'>{t('trades.picker.none')}</Text>}
            </div>

            <UITextInput
                name='trade-picker-message'
                label={t('trades.offer.message')}
                value={message}
                maxLength={300}
                onChange={e => setMessage(e.currentTarget.value)}
            />

            {result && !result.success && result.error !== undefined && <Alert variant='outline' color='yellow' icon={<TriangleAlertIcon />}>
                {t(tradeErrorKeys[ result.error ])} {result.errorDetail}
            </Alert>}

            <Group justify='flex-end'>
                <UIButton name='trade-picker-cancel' controlLabel={t('action.back')} variant='default' onClick={onClose}>
                    {t('action.back')}
                </UIButton>
                <UIButton
                    name='trade-picker-submit'
                    controlLabel={t('trades.picker.submit')}
                    variant='filled'
                    leftSection={<SendIcon />}
                    disabled={!selected}
                    loading={counterMutation.isPending}
                    onClick={submit}
                >
                    {t('trades.picker.submit')}
                </UIButton>
            </Group>
        </Stack>
    </FocusScope>;
};
