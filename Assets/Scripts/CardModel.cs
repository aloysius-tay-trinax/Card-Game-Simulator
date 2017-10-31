﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

public delegate void OnDoubleClickDelegate(CardModel cardModel);
public delegate void SecondaryDragDelegate();

public enum DragPhase
{
    Begin,
    Drag,
    End
}

[RequireComponent(typeof(Image), typeof(CanvasGroup), typeof(LayoutElement))]
public class CardModel : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, ISelectHandler, IDeselectHandler, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    public const float MovementSpeed = 600f;
    public const float AlphaHitTestMinimumThreshold = 0.01f;

    public bool IsProcessingSecondaryDragAction {
        get { return PointerPositions.Count > 1 || (CurrentPointerEventData != null && CurrentPointerEventData.button == PointerEventData.InputButton.Right); }
    }

    public CardStack ParentCardStack {
        get { return this.transform.parent.GetComponent<CardStack>(); }
    }

    public Vector2 OutlineHighlightDistance {
        get { return new Vector2(10, 10); }
    }

    public bool DidSelectOnDown { get; private set; }

    public OnDoubleClickDelegate DoubleClickAction { get; set; }

    public bool DoesCloneOnDrag { get; set; }

    public SecondaryDragDelegate SecondaryDragAction { get; set; }

    public PointerEventData CurrentPointerEventData { get; private set; }

    public DragPhase CurrentDragPhase { get; private set; }

    private Card _value;
    private Dictionary<int, Vector2> _pointerPositions;
    private Dictionary<int, CardModel> _draggedClones;
    private Dictionary<int, Vector2> _pointerDragOffsets;
    private CardStack _placeHolderCardStack;
    private RectTransform _placeHolder;
    private bool _isFacedown;
    private Outline _highlight;
    private Sprite _newSprite;

    public CardModel Clone(Transform parent)
    {
        CardModel clone = Instantiate(this.gameObject, this.transform.position, this.transform.rotation, parent).GetOrAddComponent<CardModel>();
        clone.Value = this.Value;
        clone.HideHighlight();
        return clone;
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        // HACK TO SELECT ON DOWN WHEN THE CARD INFO VIEWER IS VISIBLE; CAN'T USE CARDINFOVIEWER.ISVISIBLE SINCE IT IS SET TO FALSE WHEN POINTER DOWN, BEFORE THIS METHOD IS CALLED
        DidSelectOnDown = eventData.button != PointerEventData.InputButton.Right && CardInfoViewer.Instance.SelectedCardModel != this && ((RectTransform)CardInfoViewer.Instance.infoPanel).anchorMax.y < (CardInfoViewer.HiddenYMax + CardInfoViewer.VisibleYMax) / 2.0f;
        if (DidSelectOnDown)
            EventSystem.current.SetSelectedGameObject(this.gameObject, eventData);

        PointerPositions [eventData.pointerId] = eventData.position;

        CurrentPointerEventData = eventData;
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        if (CurrentPointerEventData != null && CurrentPointerEventData.pointerId == eventData.pointerId && eventData.button != PointerEventData.InputButton.Right && !eventData.dragging && !DraggedClones.ContainsKey(eventData.pointerId)) {
            if (!DidSelectOnDown && EventSystem.current.currentSelectedGameObject == this.gameObject && DoubleClickAction != null)
                DoubleClickAction(this);
            else if (PlaceHolder == null)
                EventSystem.current.SetSelectedGameObject(this.gameObject, eventData);
        }
        
        CurrentPointerEventData = eventData;
        CurrentDragPhase = DragPhase.End;

        if (!IsProcessingSecondaryDragAction)
            UpdatePosition();
        else if (SecondaryDragAction != null)
            SecondaryDragAction();
        if (PointerPositions.ContainsKey(eventData.pointerId))
            PointerPositions.Remove(eventData.pointerId);
    }

    public void OnSelect(BaseEventData eventData)
    {
        if (!IsFacedown)
            CardInfoViewer.Instance.SelectedCardModel = this;
    }

    public void OnDeselect(BaseEventData eventData)
    {
        CardInfoViewer.Instance.IsVisible = false;
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        EventSystem.current.SetSelectedGameObject(null, eventData);

        CardModel cardModel = this;
        if (DoesCloneOnDrag) {
            DraggedClones [eventData.pointerId] = Clone(this.gameObject.FindInParents<Canvas>().transform);
            cardModel = DraggedClones [eventData.pointerId];
            cardModel.GetComponent<CanvasGroup>().blocksRaycasts = false;
        }

        cardModel.CurrentPointerEventData = eventData;
        cardModel.CurrentDragPhase = DragPhase.Begin;

        cardModel.PointerPositions [eventData.pointerId] = eventData.position;
        cardModel.PointerDragOffsets [eventData.pointerId] = ((Vector2)cardModel.transform.position) - eventData.position;
        if (!IsProcessingSecondaryDragAction)
            cardModel.UpdatePosition();
        else if (cardModel.SecondaryDragAction != null)
            cardModel.SecondaryDragAction();
    }

    public void OnDrag(PointerEventData eventData)
    {
        CardModel cardModel;
        if (!DraggedClones.TryGetValue(eventData.pointerId, out cardModel))
            cardModel = this;
        
        cardModel.CurrentPointerEventData = eventData;
        cardModel.CurrentDragPhase = DragPhase.Drag;

        cardModel.PointerPositions [eventData.pointerId] = eventData.position;
        if (!IsProcessingSecondaryDragAction)
            cardModel.UpdatePosition();
        else if (cardModel.SecondaryDragAction != null)
            cardModel.SecondaryDragAction();
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        CardModel cardModel;
        if (!DraggedClones.TryGetValue(eventData.pointerId, out cardModel))
            cardModel = this;
        else
            DraggedClones.Remove(eventData.pointerId);

        cardModel.CurrentPointerEventData = eventData;
        cardModel.PointerDragOffsets.Remove(eventData.pointerId);

        if (!cardModel.IsProcessingSecondaryDragAction) {
            if (cardModel.PlaceHolder != null)
                cardModel.StartCoroutine(cardModel.MoveToPlaceHolder());
            else if (cardModel.ParentCardStack == null)
                Destroy(cardModel.gameObject);
        }
    }

    public void UpdatePosition()
    {
        if (CurrentPointerEventData == null || !PointerPositions.ContainsKey(CurrentPointerEventData.pointerId) || !PointerDragOffsets.ContainsKey(CurrentPointerEventData.pointerId))
            return;

        Vector2 targetPosition = PointerPositions [CurrentPointerEventData.pointerId] + PointerDragOffsets [CurrentPointerEventData.pointerId];
        if (ParentCardStack != null)
            UpdatePositionInCardStack(targetPosition);
        else
            this.transform.position = targetPosition;

        if (PlaceHolderCardStack != null)
            PlaceHolderCardStack.UpdateLayout(PlaceHolder, targetPosition);
    }

    public void UpdatePositionInCardStack(Vector2 targetPosition)
    {
        CardStack cardStack = ParentCardStack;
        if (cardStack == null)
            return;

        if (cardStack.type != CardStackType.Horizontal)
            cardStack.UpdateLayout(this.transform as RectTransform, targetPosition);

        RectTransform stackRT = cardStack.transform as RectTransform;
        Vector3[] stackCorners = new Vector3[4];
        stackRT.GetWorldCorners(stackCorners);
        bool isOutYBounds = targetPosition.y < stackCorners [0].y || targetPosition.y > stackCorners [1].y;
        switch (cardStack.type) {
            case CardStackType.Full:
                ParentToCanvas();
                this.transform.position = targetPosition;
                break;
            case CardStackType.Vertical:
                if (!IsProcessingSecondaryDragAction) {
                    if (cardStack.scrollRectContainer != null)
                        cardStack.scrollRectContainer.OnEndDrag(CurrentPointerEventData);
                    ParentToCanvas();
                    this.transform.position = targetPosition;
                }
                break;
            case CardStackType.Horizontal:
                cardStack.UpdateScrollRect(CurrentDragPhase, CurrentPointerEventData);
                if (isOutYBounds) {
                    if (cardStack.scrollRectContainer != null)
                        cardStack.scrollRectContainer.OnEndDrag(CurrentPointerEventData);
                    ParentToCanvas();
                    this.transform.position = targetPosition;
                }
                break;
            case CardStackType.Area:
            default:
                if (isOutYBounds)
                    ParentToCanvas();
                this.transform.position = targetPosition;
                break;
        }
    }

    public void ParentToCanvas()
    {
        CardStack prevParentStack = ParentCardStack;
        if (prevParentStack != null)
            PlaceHolderCardStack = prevParentStack;
        this.transform.SetParent(this.gameObject.FindInParents<Canvas>().transform);
        this.transform.SetAsLastSibling();
        if (prevParentStack != null)
            prevParentStack.OnRemove(this);
        GetComponent<CanvasGroup>().blocksRaycasts = false;
    }

    public IEnumerator MoveToPlaceHolder()
    {
        while (PlaceHolder != null && Vector3.Distance(this.transform.position, PlaceHolder.position) > 1) {
            float step = MovementSpeed * Time.deltaTime;
            this.transform.position = Vector3.MoveTowards(this.transform.position, PlaceHolder.position, step);
            yield return null;
        }

        if (PlaceHolder == null) {
            Destroy(this.gameObject);
            yield break;
        }

        this.gameObject.GetOrAddComponent<LayoutElement>().ignoreLayout = false;
        CardStack prevParentStack = ParentCardStack;
        this.transform.SetParent(PlaceHolder.parent);
        this.transform.SetSiblingIndex(PlaceHolder.GetSiblingIndex());
        if (prevParentStack != null)
            prevParentStack.OnRemove(this);
        if (ParentCardStack != null)
            ParentCardStack.OnAdd(this);
        PlaceHolder = null;
        GetComponent<CanvasGroup>().blocksRaycasts = true;
    }

    public void UpdateParentCardStackScrollRect()
    {
        CardStack cardStack = ParentCardStack;
        if (cardStack != null)
            cardStack.UpdateScrollRect(CurrentDragPhase, CurrentPointerEventData);
    }

    public void Rotate()
    {
        Vector2 referencePoint = this.transform.position;
        foreach (KeyValuePair<int, Vector2> pointerDragPosition in PointerPositions)
            if (pointerDragPosition.Key != CurrentPointerEventData.pointerId)
                referencePoint = pointerDragPosition.Value;
        Vector2 prevDir = (CurrentPointerEventData.position - CurrentPointerEventData.delta) - referencePoint;      
        Vector2 currDir = CurrentPointerEventData.position - referencePoint;
        this.transform.Rotate(0, 0, Vector2.SignedAngle(prevDir, currDir));
    }

    public static void ResetRotation(CardStack cardStack, CardModel cardModel)
    {
        if (cardModel == null)
            return;
        cardModel.transform.rotation = Quaternion.identity;
    }

    public static void ShowCard(CardStack cardStack, CardModel cardModel)
    {
        if (cardModel == null)
            return;
        cardModel.IsFacedown = false;
    }

    public static void HideCard(CardStack cardStack, CardModel cardModel)
    {
        if (cardModel == null)
            return;
        
        cardModel.IsFacedown = true;
        EventSystem.current.SetSelectedGameObject(null, cardModel.CurrentPointerEventData);
    }

    public static void ToggleFacedown(CardModel cardModel)
    {
        if (cardModel == null)
            return;
        
        cardModel.IsFacedown = !cardModel.IsFacedown;
        EventSystem.current.SetSelectedGameObject(null, cardModel.CurrentPointerEventData);
    }

    public void ShowHighlight()
    {
        Highlight.effectColor = Color.green;
        Highlight.effectDistance = OutlineHighlightDistance;
    }

    public void HideHighlight()
    {
        Highlight.effectColor = Color.black;
        Highlight.effectDistance = Vector2.zero;
    }

    public IEnumerator UpdateImage()
    {
        Sprite newSprite = null;
        yield return UnityExtensionMethods.RunOutputCoroutine<Sprite>(UnityExtensionMethods.CreateAndOutputSpriteFromImageFile(Value.ImageFilePath, Value.ImageWebURL), output => newSprite = output);
        if (newSprite != null)
            NewSprite = newSprite;
        else
            GetComponent<Image>().sprite = CardGameManager.Current.CardBackImageSprite;
    }

    void OnDestroy()
    {
        PlaceHolder = null;
        NewSprite = null;
    }

    void OnApplicationQuit()
    {
        PlaceHolder = null;
        NewSprite = null;
    }

    public Card Value {
        get {
            if (_value == null)
                _value = Card.Blank;
            return _value;
        }
        set {
            _value = value;
            if (_value == null)
                _value = Card.Blank;
            this.gameObject.name = _value.Name + " [" + _value.Id + "]";
            StartCoroutine(UpdateImage());
        }
    }

    public Dictionary<int, Vector2> PointerPositions {
        get {
            if (_pointerPositions == null)
                _pointerPositions = new Dictionary<int, Vector2>();
            return _pointerPositions;
        }
    }

    public Dictionary<int, CardModel> DraggedClones {
        get {
            if (_draggedClones == null)
                _draggedClones = new Dictionary<int, CardModel>();
            return _draggedClones;
        }
    }

    public Dictionary<int, Vector2> PointerDragOffsets {
        get {
            if (_pointerDragOffsets == null)
                _pointerDragOffsets = new Dictionary<int, Vector2>();
            return _pointerDragOffsets;
        }
    }

    public CardStack PlaceHolderCardStack {
        get {
            return _placeHolderCardStack;
        }
        set {
            _placeHolderCardStack = value;

            if (_placeHolderCardStack == null) {
                PlaceHolder = null;
                return;
            }

            GameObject placeholder = new GameObject(this.gameObject.name + "(PlaceHolder)", typeof(RectTransform));
            PlaceHolder = placeholder.transform as RectTransform;
            PlaceHolder.SetParent(_placeHolderCardStack.transform);
            PlaceHolder.sizeDelta = ((RectTransform)this.transform).sizeDelta;
            PlaceHolder.anchoredPosition = Vector2.zero;
        }
    }

    public RectTransform PlaceHolder {
        get {
            return _placeHolder;
        }
        private set {
            if (_placeHolder != null)
                Destroy(_placeHolder.gameObject);
            _placeHolder = value;
            if (_placeHolder == null)
                _placeHolderCardStack = null;
        }
    }

    public bool IsFacedown {
        get {
            return _isFacedown;
        }
        set {
            _isFacedown = value;
            if (_isFacedown)
                GetComponent<Image>().sprite = CardGameManager.Current.CardBackImageSprite;
            else if (NewSprite != null)
                GetComponent<Image>().sprite = NewSprite;
        }
    }

    public Outline Highlight {
        get {
            if (_highlight == null)
                _highlight = this.gameObject.GetOrAddComponent<Outline>();
            return _highlight;
        }
    }

    public Sprite NewSprite {
        get {
            return _newSprite;
        }
        set {
            if (_newSprite != null) {
                Destroy(_newSprite.texture);
                Destroy(_newSprite);
            }
            _newSprite = value;
            if (_newSprite != null && !IsFacedown)
                GetComponent<Image>().sprite = _newSprite;
            // TODO: FIX THIS; WILL SOMETIMES CLICK ON A NONTRANSPARENT PORTION OF A TRANSPARENT IMAGE, AND THE CLICK DOES NOT REGISTER
            GetComponent<Image>().alphaHitTestMinimumThreshold = AlphaHitTestMinimumThreshold;
        }
    }
}
