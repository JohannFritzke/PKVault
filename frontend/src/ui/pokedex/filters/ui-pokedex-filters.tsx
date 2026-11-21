import { Box, Card, Stack, Title } from '@mantine/core';
import type React from 'react';
import { useTranslate } from '../../../translate/i18n';
import { WithControlsIcons, type WithControlsIconsExtraProps } from '../../interaction/controls/icons/with-controls-icons';
import { getSelectControl } from '../../interaction/focus-controls/common-controls/select-controls';
import { useFocusControls } from '../../interaction/focus-controls/use-focus-controls';
import { Focus } from '../../interaction/focus/provider/use-focus-context';
import { FocusScope } from '../../interaction/focus/scope/focus-scope';
import { getScrollPadding } from '../../scrollbar-width/util/get-scroll-padding';
import classes from './ui-pokedex-filters.module.css';

type UIPokedexFiltersProps = {
    /** species search, shown first */
    species?: React.ReactNode;
    views: React.ReactNode;
    children: React.ReactNode;
} & WithControlsIconsExtraProps;

export const UIPokedexFilters: React.FC<UIPokedexFiltersProps> = ({ species, views, children, ...rest }) => {
    const { t } = useTranslate();

    const name = 'pokedex-filters';
    const childScopeId = name;

    const isInScopeStack = Focus.useIsInScopeStack(childScopeId);

    const { pushScope } = Focus.usePushPopScope();

    const { focusProps, nodeId, controlProps, controlIcons } = useFocusControls({
        scopeNodeId: name,
        childScopeId,
        controls: [
            !isInScopeStack && getSelectControl({
                label: t('action.select'),
                action: () => pushScope(childScopeId),
            }),
        ],
    });

    return <FocusScope id={name} parentNodeId={nodeId}>
        <WithControlsIcons placement='out' icons={controlIcons('open')} {...rest}>
            <Card {...focusProps} {...controlProps('open')}
                w='100%' mah='100%' shadow='none' radius='lg' p='lg'
                pr={getScrollPadding('lg')}
                style={{ overflowY: 'auto', scrollbarGutter: 'stable' }}
            >
                <Stack gap='lg'>
                    <Title order={2} className={classes.title}>{t('dex.filters.title')}</Title>

                    {species}

                    <UIPokedexFilterGroup label={t('dex.filters.views')}>
                        {views}
                    </UIPokedexFilterGroup>

                    {children}
                </Stack>
            </Card>
        </WithControlsIcons>
    </FocusScope>;
};

type UIPokedexFilterGroupProps = {
    label: React.ReactNode;
    children: React.ReactNode;
};

/**
 * Labelled group of filter controls, label styled like the inputs ones.
 */
export const UIPokedexFilterGroup: React.FC<UIPokedexFilterGroupProps> = ({ label, children }) => {
    return <Box role='group' aria-label={typeof label === 'string' ? label : undefined}>
        <div className={classes.groupLabel}>{label}</div>

        <Stack gap='xs'>
            {children}
        </Stack>
    </Box>;
};
