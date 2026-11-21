import { Badge, Box, Button, Group, Tooltip } from '@mantine/core';
import { useMergedRef } from '@mantine/hooks';
import React from 'react';
import { useTranslate } from '../../../translate/i18n';
import { WithControlsIcons } from '../../interaction/controls/icons/with-controls-icons';
import { getSelectControl } from '../../interaction/focus-controls/common-controls/select-controls';
import { useFocusControls } from '../../interaction/focus-controls/use-focus-controls';
import { UISpeciesImgSkeleton } from '../../sprite-img/species-img/ui-species-img-skeleton';
import { useVisibilityContext } from '../../visibility/visibility-context';
import classes from './ui-pokedex-item.module.css';

export type UIPokedexItemRawProps = {
    ref?: React.Ref<HTMLDivElement>;
    id: string;
    // context: EntityContext;
    species: number;
    form?: string;
    label: string;
    selected?: boolean;
    /** species not seen yet: muted card, name hidden */
    unseen?: boolean;
    onClick?: () => void;
    children: React.ReactNode;
};

export const UIPokedexItemRaw: React.FC<UIPokedexItemRawProps> = ({
    ref: refRoot, id, species, form,
    label, selected, unseen, onClick, children
}) => {
    const { t } = useTranslate();

    const visible = useVisibilityContext() ?? true;

    const { focusProps, controlProps, controlIcons } = useFocusControls({
        scopeNodeId: id,
        controls: [
            onClick && getSelectControl({
                label: t('action.open'),
                action: e => {
                    onClick();
                },
            }),
        ],
    });

    const ref = useMergedRef(
        refRoot,
        focusProps.ref,
        controlProps('open').ref,
    );

    return <WithControlsIcons placement='out' icons={controlIcons('open')}
        className={classes.uiPokedexItem}
    >
        <Tooltip label={label} withArrow position="bottom" disabled={!onClick || !visible}>
            <Button
                {...focusProps}
                {...controlProps('open')}
                ref={ref}
                data-dex-item
                data-selected={selected || undefined}
                data-unseen={unseen || undefined}
                variant='default'
                className={classes.button}
                maw='100%'
            >
                <Box className={classes.content}>
                    <Box className={classes.header}>
                        <span>#{String(species).padStart(4, '0')}</span>
                        {form && <Badge variant='light' color='gray' size='xs' radius='sm'>{form}</Badge>}
                    </Box>

                    <Group gap='sm' wrap='wrap' justify='center'>
                        {visible
                            ? children
                            : React.Children.map(children, (_, i) => <UISpeciesImgSkeleton key={i} animate={false} visible={false} />)}
                    </Group>

                    <span className={classes.name} data-unseen={unseen || undefined}>
                        {unseen ? '???' : label}
                    </span>
                </Box>
            </Button>
        </Tooltip>
    </WithControlsIcons>;
};
